using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Roma.Data;

namespace Roma.Services
{
    public class BackgroundAiProcessor : BackgroundService
    {
        private readonly Channel<AiJob> _channel;
        private readonly IServiceProvider _provider;
        private readonly ILogger<BackgroundAiProcessor> _logger;

        public BackgroundAiProcessor(Channel<AiJob> channel, IServiceProvider provider, ILogger<BackgroundAiProcessor> logger)
        {
            _channel = channel;
            _provider = provider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var reader = _channel.Reader;
            while (await reader.WaitToReadAsync(stoppingToken))
            {
                while (reader.TryRead(out var job))
                {
                    try
                    {
                        using var scope = _provider.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var vision = scope.ServiceProvider.GetService<IVisionService>();

                        if (job.JobType == "AltText" && vision != null)
                        {
                            var img = await db.DestinationImages!.FirstOrDefaultAsync(i => i.Id == job.ImageId);
                            if (img != null)
                            {
                                var webRoot = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>().WebRootPath;
                                var rel = img.Url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                                var full = Path.Combine(webRoot, rel);
                                if (System.IO.File.Exists(full))
                                {
                                    using var fs = System.IO.File.OpenRead(full);
                                    var alt = await vision.GenerateAltTextAsync(fs);
                                    // persist alt-text on the image record
                                    img.AltText = alt;
                                    db.Update(img);
                                    await db.SaveChangesAsync();
                                }
                            }
                        }
                        else if (job.JobType == "Description")
                        {
                            var ai = scope.ServiceProvider.GetService<IAiService>();
                            if (ai != null)
                            {
                                var dest = await db.Destinations!.FirstOrDefaultAsync(d => d.Id == job.DestinationId);
                                if (dest != null)
                                {
                                    var prompt = job.Payload ?? dest.Name + " " + dest.Country;
                                    var generated = await ai.GenerateDescriptionAsync(prompt);
                                    dest.Description = generated;
                                    db.Update(dest);
                                    await db.SaveChangesAsync();
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "AI job processing failed");
                    }
                }
            }
        }
    }
}
