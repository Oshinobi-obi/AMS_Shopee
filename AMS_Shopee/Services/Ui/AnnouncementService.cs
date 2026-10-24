// This gets the notices AMS posts in e-PINS, shown here as a banner.

using AMS_Shopee.Data;
using Microsoft.EntityFrameworkCore;

namespace AMS_Shopee.Services.Ui;

public sealed class LiveAnnouncement
{
    public uint Id { get; set; }
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string Level { get; set; } = "info";
    public DateTime StartsAt { get; set; }      // Philippine time
    public DateTime? EndsAt { get; set; }
}

public sealed class AnnouncementService(IDbContextFactory<AmsDbContext> dbFactory, ILogger<AnnouncementService> log)
{
    public async Task<List<LiveAnnouncement>> LiveAsync(CancellationToken ct = default)
    {
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, AMS_Shopee.Services.PhZone.Manila);
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            return await db.Database.SqlQuery<LiveAnnouncement>($@"
                SELECT announcement_id AS Id, title AS Title, message AS Message, level AS Level,
                       starts_at AS StartsAt, ends_at AS EndsAt
                  FROM announcements
                 WHERE is_active = 1 AND starts_at <= {now} AND (ends_at IS NULL OR ends_at >= {now})
                 ORDER BY FIELD(level, 'urgent', 'warning', 'info'), starts_at DESC
                 LIMIT 3").ToListAsync(ct);
        }
        catch (Exception ex)
        {
            // The table isn't made yet (008_announcements.sql) or the database had a problem, so just show none.
            if (!ex.GetBaseException().Message.Contains("doesn't exist")) log.LogWarning(ex, "Announcements failed to load");
            return [];
        }
    }
}
