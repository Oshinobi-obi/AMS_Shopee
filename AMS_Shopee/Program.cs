using Microsoft.AspNetCore.DataProtection;
using AMS_Shopee.Components;
using AMS_Shopee.Components.Shared.Modals;
using AMS_Shopee.Data;
using AMS_Shopee.Endpoints;
using AMS_Shopee.Services.Catalog;
using AMS_Shopee.Services.Requisition;
using AMS_Shopee.Services.Ui;
using AMS_Shopee.Services.Security;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// The login cookies are encrypted with keys. On a web host the default key folder may get
// wiped on restart (everyone gets logged out), so I keep the keys in the site's App_Data.
builder.Services.AddDataProtection()
    .SetApplicationName("AMS_Supplies")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys")));

// Blazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Database (in Development the connection string comes from User Secrets)
var connectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException(
        "ConnectionStrings:Default is empty. Run: dotnet user-secrets set \"ConnectionStrings:Default\" \"server=localhost;port=3306;database=ams_stockwatch;user=...;password=...;\"");

// I use a factory instead of AddDbContext because Blazor Server circuits stay open a long time,
// so every operation makes its own short-lived context and disposes it.
builder.Services.AddDbContextFactory<AmsDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.Parse("8.0.46-mysql")));

// Login (the cookie is set by /account/login)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/";
        o.AccessDeniedPath = "/";
        o.ExpireTimeSpan = TimeSpan.FromHours(9);   // one office workday
        // Check the account again every 5 minutes: deactivated, reset or changed password means logged out
        o.Events.OnValidatePrincipal = SessionGuard.ValidatePrincipalAsync;
        o.SlidingExpiration = true;
        o.Cookie.Name = "ams_shopee_auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        // Secure-only cookies need HTTPS. "Auth:AllowHttpCookies": true is only TEMPORARY for a
        // server that has no SSL certificate yet. Set it back to false once the site has HTTPS.
        o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() || builder.Configuration.GetValue("Auth:AllowHttpCookies", false)
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
// Open pages also check the account every 5 minutes
builder.Services.AddScoped<AuthenticationStateProvider, RevalidatingAuthStateProvider>();

// Login rate limit per computer (IP address)
// 40 tries per 5 minutes is enough for a whole office on one internet connection,
// but way too few for a password guessing bot. Each account also locks after 5 wrong tries.
builder.Services.AddRateLimiter(o =>
{
    o.AddPolicy("login", http => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
        {
            PermitLimit = 40,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0,
        }));
    o.OnRejected = (ctx, _) =>
    {
        ctx.HttpContext.Response.StatusCode = StatusCodes.Status303SeeOther;
        ctx.HttpContext.Response.Headers.Location = "/?login=busy";
        return ValueTask.CompletedTask;
    };
});


// App services
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ModalService>();
builder.Services.AddScoped<RequisitionService>();
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<OfficeQueries>();
builder.Services.AddScoped<MyRequisitionQueries>();
builder.Services.AddScoped<AMS_Shopee.Services.Accountability.AccountabilityQueries>();
builder.Services.AddSingleton<AMS_Shopee.Services.Accountability.BirthdayGate>();
builder.Services.AddHttpClient(AMS_Shopee.Endpoints.AccountabilityEndpoints.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddScoped<AMS_Shopee.Services.Ui.AnnouncementService>();
builder.Services.AddSingleton<AMS_Shopee.Services.Ui.SeasonState>();
// Live updates: one database watcher for the server, one relay per browser tab
builder.Services.AddSingleton<AMS_Shopee.Services.Live.RisEventFeed>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<AMS_Shopee.Services.Live.RisEventFeed>());
builder.Services.AddScoped<AMS_Shopee.Services.Live.LiveRefresh>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<CartState>();
builder.Services.AddScoped<AddToCartFlow>();

var app = builder.Build();

// HTTP pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
if (!app.Configuration.GetValue("Auth:AllowHttpCookies", false))
    app.UseHttpsRedirection();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
// Security headers on every response
app.Use(async (context, next) =>
{
    var h = context.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";                         // browsers shouldn't guess file types
    h["X-Frame-Options"] = "SAMEORIGIN";                             // other websites can't show this site in a frame
    h["Content-Security-Policy"] = "frame-ancestors 'self'";
    h["Referrer-Policy"] = "strict-origin-when-cross-origin";
    h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
    h["Cross-Origin-Opener-Policy"] = "same-origin";
    await next();
});

app.UseAntiforgery();

app.MapStaticAssets();
app.MapAccountEndpoints();
app.MapRisEndpoints();
app.MapAccountabilityEndpoints();
// Live update status (open /live/status while logged in)
// Shows if the 5-second watcher is running and the last error, if there is one.
app.MapGet("/live/status", (AMS_Shopee.Services.Live.RisEventFeed feed) =>
{
    static string? Ph(DateTime? utc) => utc is { } u ? TimeZoneInfo.ConvertTimeFromUtc(u, AMS_Shopee.Services.PhZone.Manila).ToString("yyyy-MM-dd hh:mm:ss tt") : null;
    var running = feed.LastCheckUtc is { } last && DateTime.UtcNow - last < TimeSpan.FromSeconds(30);
    return Results.Json(new
    {
        running,
        meaning = running ? "Live updates are working." : "The watcher has not checked in the last 30 seconds. Restart the site and see lastError.",
        lastCheck = Ph(feed.LastCheckUtc),
        lastChange = Ph(feed.LastChangeUtc),
        lastChangedAreas = feed.LastChangedAreas,
        lastError = feed.LastError,
        lastErrorAt = Ph(feed.LastErrorUtc),
        watching = feed.Signatures.Keys.OrderBy(k => k).ToArray(),
    });
}).RequireAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
