using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Roma.Models;
using Roma.Services;
using System.Text.Json;

namespace Roma.Data
{
    public class ApplicationDbContext : IdentityDbContext<Roma.Models.User>
    {
        private readonly Services.IEmailQueue? _emailQueue;
        private readonly IConfiguration? _configuration;
        private readonly Services.IEmailComposer? _emailComposer;

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, Services.IEmailQueue? emailQueue = null, IConfiguration? configuration = null, Services.IEmailComposer? emailComposer = null) : base(options)
        {
            _emailQueue = emailQueue;
            _configuration = configuration;
            _emailComposer = emailComposer;
        }

        public DbSet<Destination>? Destinations { get; set; }
        public DbSet<DestinationImage>? DestinationImages { get; set; }
        public DbSet<DownloadRecord> DownloadRecords { get; set; }
        public DbSet<Attraction>? Attractions { get; set; }
        public DbSet<Itinerary>? Itineraries { get; set; }
        public DbSet<Review>? Reviews { get; set; }
        public DbSet<Meal>? Meals { get; set; }
        public DbSet<Flavor>? Flavors { get; set; }
        public DbSet<Hotel>? Hotels { get; set; }
        public DbSet<Parking>? Parkings { get; set; }
        public DbSet<Recommendation>? Recommendations { get; set; }
        public DbSet<AuditLog>? AuditLogs { get; set; }
        public DbSet<Subscriber>? Subscribers { get; set; }
        public DbSet<FailedEmail>? FailedEmails { get; set; }
        public DbSet<Feedback>? Feedbacks { get; set; }
        public DbSet<TwoFactorCode>? TwoFactorCodes { get; set; }
        public DbSet<DemoChatService>? DemoChatServices { get; set; }
        public DbSet<AiBudgetUsageStat>? AiBudgetUsageStats { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Decimal precision
            modelBuilder.Entity<Destination>().Property(d => d.EntranceFee).HasPrecision(18, 2);
            modelBuilder.Entity<Meal>().Property(m => m.Price).HasPrecision(18, 2);
            modelBuilder.Entity<Hotel>().Property(h => h.PriceFrom).HasPrecision(18, 2);
            modelBuilder.Entity<Hotel>().Property(h => h.PriceTo).HasPrecision(18, 2);
            modelBuilder.Entity<Parking>().Property(p => p.Fee).HasPrecision(18, 2);
            modelBuilder.Entity<Parking>().Property(p => p.HourlyRate).HasPrecision(18, 2);
            modelBuilder.Entity<DownloadRecord>().Property(d => d.Amount).HasPrecision(18, 2);
            modelBuilder.Entity<AiBudgetUsageStat>().Property(x => x.EstimatedCostUsd).HasPrecision(18, 4);
            modelBuilder.Entity<AiBudgetUsageStat>().HasIndex(x => x.MonthKey).IsUnique();

            // Subscriber: unique email
            modelBuilder.Entity<Subscriber>().HasIndex(s => s.Email).IsUnique();

            modelBuilder.Entity<FailedEmail>().HasIndex(f => f.CreatedAt);
            modelBuilder.Entity<Subscriber>().Property(s => s.UnsubscribeToken).HasMaxLength(100);
            modelBuilder.Entity<Feedback>().HasIndex(f => f.CreatedAt);

            // Ensure cascade delete for dependent entities of Destination
            modelBuilder.Entity<DestinationImage>().HasOne(di => di.Destination).WithMany(d => d.Images).HasForeignKey(di => di.DestinationId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<DestinationImage>().Property(di => di.Order).HasDefaultValue(0);
            modelBuilder.Entity<Meal>().HasOne(m => m.Destination).WithMany(d => d.Meals).HasForeignKey(m => m.DestinationId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Flavor>().HasOne(f => f.Destination).WithMany(d => d.Flavors).HasForeignKey(f => f.DestinationId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Hotel>().HasOne(h => h.Destination).WithMany(d => d.Hotels).HasForeignKey(h => h.DestinationId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Parking>().HasOne(p => p.Destination).WithMany(d => d.Parkings).HasForeignKey(p => p.DestinationId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Recommendation>().HasOne(r => r.Destination).WithMany(d => d.Recommendations).HasForeignKey(r => r.DestinationId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Attraction>().HasOne(a => a.Destination).WithMany(d => d.Attractions).HasForeignKey(a => a.DestinationId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Review>().HasOne(r => r.Attraction).WithMany(a => a.Reviews).HasForeignKey(r => r.AttractionId).OnDelete(DeleteBehavior.Cascade);
        }

        public override int SaveChanges()
        {
            AddAuditLogs().GetAwaiter().GetResult();
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // detect newly added destinations before saving
            var addedDestinations = ChangeTracker.Entries<Destination>()
                .Where(e => e.State == EntityState.Added)
                .Select(e => e.Entity)
                .ToList();

            await AddAuditLogs();

            var result = await base.SaveChangesAsync(cancellationToken);

            // after save, send notifications for added destinations (fire-and-forget)
            if (_emailQueue != null && addedDestinations.Count > 0)
            {
                _ = NotifySubscribersAsync(addedDestinations);
            }

            return result;
        }

        private async Task NotifySubscribersAsync(List<Destination> newDestinations)
        {
            try
            {
                var subscribers = await Subscribers!.Where(s => s.IsConfirmed && s.ConsentGiven).ToListAsync();
                if (subscribers.Count == 0) return;

                var baseUrl = _configuration?[("App:BaseUrl")]?.TrimEnd('/') ?? string.Empty;
                foreach (var dest in newDestinations)
                {
                    var subject = $"Yeni destinasyon eklendi: {dest.Name}";

                    foreach (var sub in subscribers)
                    {
                        try
                        {
                            // ensure unsubscribe token exists
                            if (string.IsNullOrEmpty(sub.UnsubscribeToken))
                            {
                                sub.UnsubscribeToken = System.Guid.NewGuid().ToString("N");
                                // persist token
                                this.Subscribers!.Update(sub);
                                await this.SaveChangesAsync();
                            }

                            var unsubscribeLink = string.IsNullOrEmpty(baseUrl)
                                ? $"/Unsubscribe?email={System.Uri.EscapeDataString(sub.Email)}&token={sub.UnsubscribeToken}"
                                : $"{baseUrl}/Unsubscribe?email={System.Uri.EscapeDataString(sub.Email)}&token={sub.UnsubscribeToken}";

                            var destLink = string.IsNullOrEmpty(baseUrl)
                                ? $"/Destinations/Details/{dest.Id}"
                                : $"{baseUrl}/Destinations/Details/{dest.Id}";

                            // Compose email: prefer IEmailComposer when available, otherwise use template or fallback HTML/plain text
                            string? body = null;
                            if (_emailComposer != null)
                            {
                                try
                                {
                                    body = await _emailComposer.ComposeNewDestinationHtmlAsync(dest.Name, dest.Description ?? string.Empty, destLink);
                                }
                                catch
                                {
                                    // fall through to template/fallback if composer fails
                                    body = null;
                                }
                            }

                            if (string.IsNullOrEmpty(body))
                            {
                                var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "templates", "destination.html");
                                if (File.Exists(templatePath))
                                {
                                    try
                                    {
                                        var tpl = File.ReadAllText(templatePath);
                                        var logoUrl = string.IsNullOrEmpty(baseUrl) ? "/images/logo.svg" : $"{baseUrl}/images/logo.svg";
                                        var replaced = tpl.Replace("{{LogoUrl}}", logoUrl)
                                                          .Replace("{{DestName}}", System.Net.WebUtility.HtmlEncode(dest.Name))
                                                          .Replace("{{DestDesc}}", System.Net.WebUtility.HtmlEncode(dest.Description ?? string.Empty))
                                                          .Replace("{{DestLink}}", destLink)
                                                          .Replace("{{UnsubscribeLink}}", unsubscribeLink)
                                                          .Replace("{{FeedbackLink}}", string.IsNullOrEmpty(baseUrl) ? "/Feedback" : $"{baseUrl}/Feedback");
                                        body = replaced;
                                    }
                                    catch
                                    {
                                        body = $"<p>Yeni destinasyon: {System.Net.WebUtility.HtmlEncode(dest.Name)}</p><p>{System.Net.WebUtility.HtmlEncode(dest.Description ?? string.Empty)}</p><p><a href='{destLink}'>İncele</a></p><p>Abonelikten çıkmak için: <a href='{unsubscribeLink}'>tıklayın</a></p>";
                                    }
                                }
                                else
                                {
                                    var logoUrl = string.IsNullOrEmpty(baseUrl) ? "/images/logo.svg" : $"{baseUrl}/images/logo.svg";
                                    body = $@"<html><body style='font-family:Segoe UI,sans-serif'>
<div style='max-width:600px;margin:0 auto'>
  <div style='text-align:center;padding:10px 0'><img src='{logoUrl}' alt='Logo' style='max-height:60px' /></div>
  <h2>Yeni destinasyon eklendi: {System.Net.WebUtility.HtmlEncode(dest.Name)}</h2>
  <p>{System.Net.WebUtility.HtmlEncode(dest.Description ?? string.Empty)}</p>
  <p><a href='{destLink}' style='display:inline-block;padding:10px 16px;background:#0078d4;color:#fff;text-decoration:none;border-radius:4px'>İncele</a></p>
  <hr />
  <p style='font-size:12px;color:#666'>Bu e‑postayı almak istemiyorsanız <a href='{unsubscribeLink}'>abonelikten çıkın</a>.</p>
</div>
</body></html>";
                                }
                            }

                            if (_emailQueue != null)
                            {
                                await _emailQueue.EnqueueAsync(new Services.EmailMessage(sub.Email, subject, body ?? string.Empty));
                            }
                        }
                        catch
                        {
                            // ignore per-subscriber failures
                        }
                    }
                }
            }
            catch
            {
                // don't throw from DbContext notification
            }
        }

        private async Task AddAuditLogs()
        {
            var auditLogs = new List<AuditLog>();

            var entries = ChangeTracker.Entries().Where(e => e.State != EntityState.Detached && e.State != EntityState.Unchanged);
            foreach (var entry in entries)
            {
                if (entry.Entity is AuditLog) continue;

                var tableName = entry.Metadata.GetTableName();
                var keyNames = entry.Metadata.FindPrimaryKey()?.Properties.Select(p => p.Name).ToList() ?? new List<string>();

                var keyValues = new Dictionary<string, object?>();
                foreach (var key in keyNames)
                {
                    var prop = entry.Property(key);
                    keyValues[key] = prop.CurrentValue;
                }

                var oldValues = new Dictionary<string, object?>();
                var newValues = new Dictionary<string, object?>();

                foreach (var prop in entry.Properties)
                {
                    var name = prop.Metadata.Name;
                    if (prop.IsTemporary) continue; // skip temporary values (e.g., identities generated on save)
                    switch (entry.State)
                    {
                        case EntityState.Added:
                            newValues[name] = prop.CurrentValue;
                            break;
                        case EntityState.Deleted:
                            oldValues[name] = prop.OriginalValue;
                            break;
                        case EntityState.Modified:
                            if (prop.IsModified)
                            {
                                oldValues[name] = prop.OriginalValue;
                                newValues[name] = prop.CurrentValue;
                            }
                            break;
                    }
                }

                var audit = new AuditLog
                {
                    TableName = tableName ?? entry.Entity.GetType().Name,
                    KeyValues = JsonSerializer.Serialize(keyValues),
                    OldValues = oldValues.Count > 0 ? JsonSerializer.Serialize(oldValues) : null,
                    NewValues = newValues.Count > 0 ? JsonSerializer.Serialize(newValues) : null,
                    Action = entry.State.ToString(),
                    PerformedAt = DateTime.UtcNow
                };

                auditLogs.Add(audit);
            }

            if (auditLogs.Count > 0)
            {
                AuditLogs!.AddRange(auditLogs);
            }

            await Task.CompletedTask;
        }
    }
}
