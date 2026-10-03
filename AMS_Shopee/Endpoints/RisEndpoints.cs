// RisEndpoints.cs — lets an office open the signed RIS (PDF) that AMS uploaded.
// An office can only open its own requisitions.

using System.Security.Claims;
using AMS_Shopee.Data;
using Microsoft.EntityFrameworkCore;

namespace AMS_Shopee.Endpoints;

public static class RisEndpoints
{
    public static void MapRisEndpoints(this WebApplication app)
    {
        // Item photos uploaded in AMS StockWatch. Public, like the catalog itself.
        // The image address changes (?v=…) whenever AMS replaces a photo, so it can be cached.
        app.MapGet("/images/items/{id:long}", async (long id, HttpContext http, IDbContextFactory<AmsDbContext> dbFactory) =>
        {
            if (id is <= 0 or > uint.MaxValue) return Results.NotFound();
            var itemId = (uint)id;
            await using var db = await dbFactory.CreateDbContextAsync();
            var type = await db.Database
                .SqlQuery<string>($"SELECT content_type AS Value FROM item_images WHERE item_id = {itemId}")
                .FirstOrDefaultAsync();
            if (type is null) return Results.NotFound();
            var content = await db.Database
                .SqlQuery<byte[]>($"SELECT content AS Value FROM item_images WHERE item_id = {itemId}")
                .FirstAsync();
            http.Response.Headers.CacheControl = "public, max-age=604800";
            return Results.File(content, type);
        });

        app.MapGet("/ris/{id:long}/signed", async (long id, bool? download, HttpContext http, IDbContextFactory<AmsDbContext> dbFactory) =>
        {
            if (id is <= 0 or > uint.MaxValue) return Results.NotFound();
            if (!uint.TryParse(http.User.FindFirstValue("office_id"), out var officeId)) return Results.NotFound();
            var risId = (uint)id;

            await using var db = await dbFactory.CreateDbContextAsync();
            var risNo = await db.RisTransactions.AsNoTracking()
                .Where(r => r.RisId == risId && r.OfficeId == officeId)
                .Select(r => r.RisNo).FirstOrDefaultAsync();
            if (risNo is null) return Results.NotFound();

            var pdf = await db.Database
                .SqlQuery<byte[]>($"SELECT content AS Value FROM ris_signed_copies WHERE ris_id = {risId}")
                .FirstOrDefaultAsync();
            if (pdf is null) return Results.NotFound();

            return download == true
                ? Results.File(pdf, "application/pdf", $"RIS-{risNo}-signed.pdf")
                : Results.File(pdf, "application/pdf");
        }).RequireAuthorization();
    }
}
