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

// Sign-in cookies are encrypted with keys. On a web host the default key location
// may not survive restarts (everyone gets logged out), so keep them in the site's App_Data.
builder.Services.AddDataProtection()
    .SetApplicationName("AMS_Supplies")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys")));

// ── Blazor ──────────────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ── Database (connection string comes from User Secrets in Development) ─
var connectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException(
        "ConnectionStrings:Default is empty. Run: dotnet user-secrets set \"ConnectionStrings:Default\" \"server=localhost;port=3306;database=ams_stockwatch;user=...;password=...;\"");

// Factory, not AddDbContext: Blazor Server circuits are long-lived, so each
// operation creates and disposes its own short-lived context.
builder.Services.AddDbContextFactory<AmsDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.Parse("8.0.46-mysql")));

// ── Authentication (cookie set by /account/login) ──────────────────────
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/";
        o.AccessDeniedPath = "/";
        o.ExpireTimeSpan = TimeSpan.FromHours(9);   // one office workday
        // Re-check the account every 5 minutes: deactivated / reset / changed password = signed out
        o.Events.OnValidatePrincipal = SessionGuard.ValidatePrincipalAsync;
        o.SlidingExpiration = true;
        o.Cookie.Name = "ams_shopee_auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        // Secure-only cookies need HTTPS. "Auth:AllowHttpCookies": true is a TEMPORARY switch for a
        // server without an SSL certificate yet. Set it back to false once the site has HTTPS.
        o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() || builder.Configuration.GetValue("Auth:AllowHttpCookies", false)
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
// Open pages re-check the account every 5 minutes too
builder.Services.AddScoped<AuthenticationStateProvider, RevalidatingAuthStateProvider>();

// ── Sign-in rate limit per computer (IP address) ────────────────────────────
// 40 attempts per 5 minutes: plenty for a whole office behind one internet connection,
// far too few for automated password guessing. Each account also locks after 5 wrong tries.
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


// ── App services ────────────────────────────────────────────────────────
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ModalService>();
builder.Services.AddScoped<RequisitionService>();
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<OfficeQueries>();
builder.Services.AddScoped<MyRequisitionQueries>();
// Live updates: one database watcher for the server, one relay per browser tab
builder.Services.AddSingleton<AMS_Shopee.Services.Live.RisEventFeed>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<AMS_Shopee.Services.Live.RisEventFeed>());
builder.Services.AddScoped<AMS_Shopee.Services.Live.LiveRefresh>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<CartState>();
builder.Services.AddScoped<AddToCartFlow>();

var app = builder.Build();

// ── HTTP pipeline ───────────────────────────────────────────────────────
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
// ── Security headers on every response ──────────────────────────────────────
app.Use(async (context, next) =>
{
    var h = context.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";                         // browsers must not guess file types
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
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
