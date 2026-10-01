using AMS_Shopee.Components;
using AMS_Shopee.Components.Shared.Modals;
using AMS_Shopee.Data;
using AMS_Shopee.Endpoints;
using AMS_Shopee.Services.Catalog;
using AMS_Shopee.Services.Requisition;
using AMS_Shopee.Services.Ui;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

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
        o.SlidingExpiration = true;
        o.Cookie.Name = "ams_shopee_auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// ── App services ────────────────────────────────────────────────────────
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ModalService>();
builder.Services.AddScoped<RequisitionService>();
builder.Services.AddScoped<CatalogService>();
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
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapAccountEndpoints();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
