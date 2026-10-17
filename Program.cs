using Microsoft.EntityFrameworkCore;
using Roma.Data;
using System.Threading.Channels;
using Roma.Models;
using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddHttpContextAccessor();
// In-memory cache used by upload rate-limiting and other features
builder.Services.AddMemoryCache();
// Session for tracking anonymous users' first-download free rule
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromDays(7);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
// Response compression for faster transfers on mobile
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[] {
        "image/svg+xml",
        "application/javascript",
        "application/json"
    });
});
builder.Services.Configure<BrotliCompressionProviderOptions>(opts => opts.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(opts => opts.Level = CompressionLevel.Fastest);
// Image processing service
builder.Services.AddSingleton<Roma.Services.ImageProcessingService>();
// AI services (demo implementations)
builder.Services.AddSingleton<Roma.Services.IAiService, Roma.Services.DemoAiService>();
builder.Services.AddSingleton<Roma.Services.IVisionService, Roma.Services.DemoVisionService>();
builder.Services.AddSingleton<Roma.Services.IChatService, Roma.Services.DemoChatService>();
builder.Services.AddSingleton<Roma.Services.IEmailComposer, Roma.Services.DemoEmailComposer>();
// AI job channel + background processor
builder.Services.AddSingleton(Channel.CreateUnbounded<Roma.Services.AiJob>());
builder.Services.AddHostedService<Roma.Services.BackgroundAiProcessor>();
// Payment service (demo)
builder.Services.AddSingleton<Roma.Services.IPaymentService, Roma.Services.DemoPaymentService>();
// Email infrastructure: channel, queue and background sender
builder.Services.AddSingleton(Channel.CreateUnbounded<Roma.Services.EmailMessage>());
builder.Services.AddSingleton<Roma.Services.IEmailQueue, Roma.Services.EmailQueue>();
builder.Services.AddSingleton<Roma.Services.IEmailSenderService, Roma.Services.EmailSenderService>();
builder.Services.AddHostedService<Roma.Services.BackgroundEmailSender>();
builder.Services.AddDefaultIdentity<User>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

// Use response compression early
app.UseResponseCompression();

// Serve static files with caching for assets and uploaded images
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var path = ctx.Context.Request.Path.Value ?? string.Empty;
        // Cache static assets for 30 days
        if (path.StartsWith("/lib/") || path.StartsWith("/css/") || path.StartsWith("/js/") || path.StartsWith("/uploads/"))
        {
            ctx.Context.Response.Headers["Cache-Control"] = "public,max-age=2592000"; // 30 days
        }
        else
        {
            ctx.Context.Response.Headers["Cache-Control"] = "no-cache";
        }
    }
});

app.UseAuthentication();
app.UseAuthorization();

// Session middleware (after app is built and before endpoints)
app.UseSession();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

// Seed roles and optional admin user from configuration (App:AdminEmail, App:AdminPassword)
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        try
        {
            // RoleManager may not be registered when AddDefaultIdentity was used without roles.
            var roleManager = services.GetService<RoleManager<IdentityRole>>();
            var userManager = services.GetRequiredService<UserManager<User>>();
            var configuration = services.GetRequiredService<IConfiguration>();

            var adminRole = "Admin";
            var editorRole = "Editor";
            if (roleManager != null)
            {
                if (!roleManager.RoleExistsAsync(adminRole).GetAwaiter().GetResult())
                {
                    roleManager.CreateAsync(new IdentityRole(adminRole)).GetAwaiter().GetResult();
                }
                if (!roleManager.RoleExistsAsync(editorRole).GetAwaiter().GetResult())
                {
                    roleManager.CreateAsync(new IdentityRole(editorRole)).GetAwaiter().GetResult();
                }
            }

            var adminEmail = configuration["App:AdminEmail"];
            var adminPassword = configuration["App:AdminPassword"];
            if (!string.IsNullOrEmpty(adminEmail) && !string.IsNullOrEmpty(adminPassword))
            {
                var adminUser = userManager.FindByEmailAsync(adminEmail).GetAwaiter().GetResult();
                if (adminUser == null)
                {
                    adminUser = new User { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
                    var createResult = userManager.CreateAsync(adminUser, adminPassword).GetAwaiter().GetResult();
                    // ignore failures here; they will be logged
                }

                if (adminUser != null && roleManager != null && !userManager.IsInRoleAsync(adminUser, adminRole).GetAwaiter().GetResult())
                {
                    userManager.AddToRoleAsync(adminUser, adminRole).GetAwaiter().GetResult();
                }
            }

            // Optionally seed an Editor user if configured
            var editorEmail = configuration["App:EditorEmail"];
            var editorPassword = configuration["App:EditorPassword"];
            if (!string.IsNullOrEmpty(editorEmail) && !string.IsNullOrEmpty(editorPassword))
            {
                var editorUser = userManager.FindByEmailAsync(editorEmail).GetAwaiter().GetResult();
                if (editorUser == null)
                {
                    editorUser = new User { UserName = editorEmail, Email = editorEmail, EmailConfirmed = true };
                    var createResult = userManager.CreateAsync(editorUser, editorPassword).GetAwaiter().GetResult();
                }

                if (editorUser != null && roleManager != null && !userManager.IsInRoleAsync(editorUser, editorRole).GetAwaiter().GetResult())
                {
                    userManager.AddToRoleAsync(editorUser, editorRole).GetAwaiter().GetResult();
                }
            }
        }
        catch (Exception ex)
        {
            var logger = services.GetService<ILogger<Program>>();
            logger?.LogError(ex, "Error while seeding roles or admin user");
        }
    }

app.Run();
