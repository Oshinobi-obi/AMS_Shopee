// AnnouncementService.cs — notices AMS posts in StockWatch, shown as a banner here.

using AMS_Shopee.Data;
using Microsoft.EntityFrameworkCore;

namespace AMS_Shopee.Services.Ui;

public sealed class LiveAnnouncement
{
    public uint Id { get; set; }
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string Level { get; set; } = "info";
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
                SELECT announcement_id AS Id, title AS Title, message AS Message, level AS Level
                  FROM announcements
                 WHERE is_active = 1 AND starts_at <= {now} AND (ends_at IS NULL OR ends_at >= {now})
                 ORDER BY FIELD(level, 'urgent', 'warning', 'info'), starts_at DESC
                 LIMIT 3").ToListAsync(ct);
        }
        catch (Exception ex)
        {
            // Table not created yet (008_announcements.sql) or a database hiccup: just show none.
            if (!ex.GetBaseException().Message.Contains("doesn't exist")) log.LogWarning(ex, "Announcements failed to load");
            return [];
        }
    }
}
