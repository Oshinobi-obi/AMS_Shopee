// This is where e-SRS serves the PDFs of the property documents for My accountability.
//
// Every link has ?t=<pass>. The page only gets the pass after the birthday matches (BirthdayGate).
// A pass only opens that person's own documents and expires after 30 minutes.
//
//   GET /accountability/documents/{id}?t=...[&download=true]   one PDF
//   GET /accountability/documents/zip?t=...&ids=1,2,3            the selected PDFs in one ZIP
//
// The PDFs are kept by e-PINS. I get them one of two ways:
//   1. Storage:PropertyDocumentsPath points to the same folder e-PINS uses (both sites on one
//      computer), so I read the file straight from there.
//   2. Otherwise I ask e-PINS for it. StockWatch:BaseUrl is the e-PINS address, and
//      Integration:Key is a secret word with the same value in the appsettings of both sites.

using System.IO.Compression;
using AMS_Shopee.Services.Accountability;

namespace AMS_Shopee.Endpoints;

public static class AccountabilityEndpoints
{
    public const string HttpClientName = "stockwatch";
    private const int MaxZipFiles = 200;

    public static void MapAccountabilityEndpoints(this WebApplication app)
    {
        app.MapGet("/accountability/documents/zip", async (string? t, string? ids, HttpContext http,
            BirthdayGate gate, AccountabilityQueries queries, IConfiguration config, IWebHostEnvironment env,
            IHttpClientFactory clients, ILogger<AccountabilityQueries> log) =>
        {
            if (gate.ReadPass(t) is not { } person) return Expired();
            var wanted = (ids ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => uint.TryParse(s, out var v) ? v : 0).Where(v => v > 0).Distinct().Take(MaxZipFiles).ToList();
            if (wanted.Count == 0) return Results.BadRequest("Choose at least one document.");

            // Only the person's own documents
            var files = (await queries.FilesAsync(wanted)).Where(f => f.PersonnelId == person).ToList();
            if (files.Count == 0) return Results.NotFound();

            http.Response.Headers.CacheControl = "no-store";
            var temp = new FileStream(Path.GetTempFileName(), FileMode.Open, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.DeleteOnClose);
            var missing = new List<string>();
            using (var zip = new ZipArchive(temp, ZipArchiveMode.Create, leaveOpen: true))
            {
                var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var f in files)
                {
                    var bytes = await ReadAsync(f, http, config, env, clients, log);
                    if (bytes is null) { missing.Add(f.OriginalName); continue; }
                    var entry = zip.CreateEntry(UniqueName(f.OriginalName, used), CompressionLevel.NoCompression);
                    using var s = entry.Open();
                    s.Write(bytes);
                }
                if (missing.Count > 0)
                {
                    var note = zip.CreateEntry("_files-not-found.txt");
                    using var w = new StreamWriter(note.Open());
                    w.Write("These documents couldn't be found. Ask AMS for a copy:\r\n\r\n" + string.Join("\r\n", missing));
                }
            }
            if (missing.Count == files.Count)
            {
                await temp.DisposeAsync();
                return Results.Problem("None of the chosen PDFs could be found. Please tell AMS.", statusCode: 404);
            }
            temp.Position = 0;
            return Results.File(temp, "application/zip", $"My accountability {DateTime.UtcNow.AddHours(8):yyyy-MM-dd}.zip");
        });

        app.MapGet("/accountability/documents/{id:long}", async (long id, string? t, bool? download, HttpContext http,
            BirthdayGate gate, AccountabilityQueries queries, IConfiguration config, IWebHostEnvironment env,
            IHttpClientFactory clients, ILogger<AccountabilityQueries> log) =>
        {
            if (gate.ReadPass(t) is not { } person) return Expired();
            if (id is <= 0 or > uint.MaxValue) return Results.NotFound();
            var file = await queries.FileAsync((uint)id);
            if (file is null || file.PersonnelId != person) return Results.NotFound();

            http.Response.Headers.CacheControl = "private, no-store";
            var bytes = await ReadAsync(file, http, config, env, clients, log);
            return bytes is null
                ? Results.NotFound()
                : Results.File(bytes, "application/pdf", download == true ? file.OriginalName : null);
        });
    }

    private static IResult Expired() => Results.Content(
        "<!doctype html><meta charset=utf-8><title>Link expired</title><body style=\"font-family:system-ui;padding:2rem;color:#141a3a\">" +
        "<h1 style=\"font-size:1.25rem\">This link has expired</h1><p>For your privacy, document links work for 30 minutes after you enter your birthday. " +
        "Go back to <a href=\"/my-accountability\">My accountability</a> and enter your birthday again.</p>",
        "text/html", statusCode: StatusCodes.Status403Forbidden);

    private static string UniqueName(string name, HashSet<string> used)
    {
        var clean = string.Concat((string.IsNullOrWhiteSpace(name) ? "document.pdf" : name).Split(Path.GetInvalidFileNameChars()));
        var stem = Path.GetFileNameWithoutExtension(clean);
        var ext = Path.GetExtension(clean);
        var candidate = clean;
        for (var n = 2; !used.Add(candidate); n++) candidate = $"{stem} ({n}){ext}";
        return candidate;
    }

    /// Gets the PDF from the shared folder or from e-PINS. Returns null if it can't be found.
    private static async Task<byte[]?> ReadAsync(AccountabilityFile file, HttpContext http, IConfiguration config,
        IWebHostEnvironment env, IHttpClientFactory clients, ILogger log)
    {
        // 1. Same folder as e-PINS
        var folder = config["Storage:PropertyDocumentsPath"];
        if (!string.IsNullOrWhiteSpace(folder))
        {
            var path = Path.Combine(Path.GetFullPath(Path.Combine(env.ContentRootPath, folder)), Path.GetFileName(file.StoredName));
            if (File.Exists(path)) return await File.ReadAllBytesAsync(path, http.RequestAborted);
        }

        // 2. Ask e-PINS
        var baseUrl = config["StockWatch:BaseUrl"];
        var key = config["Integration:Key"];
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(key))
        {
            log.LogWarning("Property document {Id}: set StockWatch:BaseUrl and Integration:Key (or Storage:PropertyDocumentsPath) in appsettings.", file.DocumentId);
            return null;
        }
        try
        {
            var client = clients.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl.TrimEnd('/')}/integration/property-documents/{file.DocumentId}");
            request.Headers.Add("X-AMS-Key", key);
            using var response = await client.SendAsync(request, http.RequestAborted);
            if (!response.IsSuccessStatusCode)
            {
                log.LogWarning("e-PINS answered {Status} for property document {Id}", (int)response.StatusCode, file.DocumentId);
                return null;
            }
            return await response.Content.ReadAsByteArrayAsync(http.RequestAborted);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            log.LogWarning(ex, "Could not reach e-PINS for property document {Id}", file.DocumentId);
            return null;
        }
    }
}
