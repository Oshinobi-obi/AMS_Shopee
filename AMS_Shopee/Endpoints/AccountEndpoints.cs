// AccountEndpoints.cs
// Why an endpoint: an interactive (SignalR) component cannot set an auth cookie.
// The login modal renders a plain <form method="post" action="/account/login">
// with <AntiforgeryToken />, posts here, and the page reloads signed in.
//
// Program.cs:
//   builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
//       .AddCookie(o => { o.LoginPath = "/?login=1"; o.ExpireTimeSpan = TimeSpan.FromHours(9);
//                         o.SlidingExpiration = true; o.Cookie.HttpOnly = true;
//                         o.Cookie.SecurePolicy = CookieSecurePolicy.Always; });
//   builder.Services.AddCascadingAuthenticationState();
//   app.UseAuthentication(); app.UseAuthorization(); app.UseAntiforgery();
//   app.MapAccountEndpoints();
// Package: BCrypt.Net-Next

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AMS_Shopee.Endpoints;

public static class AccountEndpoints
{
    private const int MaxFailures = 5;
    private static readonly TimeSpan LockFor = TimeSpan.FromMinutes(15);
    private const int TargetBcryptCost = 12;
    // Same convention as RequisitionService: all DB timestamps are Philippine time.
    private static readonly TimeZoneInfo Manila = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");

    public sealed class LoginForm
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string? ReturnUrl { get; set; }
        public int? PendingItemId { get; set; }   // item the guest tried to add
        public int? PendingQty { get; set; }
    }

    public static void MapAccountEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/account");

        g.MapPost("/login", async ([FromForm] LoginForm f, HttpContext http,
                                   IDbContextFactory<AmsDbContext> dbf, TimeProvider clock) =>
        {
            var back = SafeLocal(f.ReturnUrl);
            await using var db = await dbf.CreateDbContextAsync();
            var now = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Manila).DateTime;
            var ip = http.Connection.RemoteIpAddress?.ToString();

            var user = await db.Users.Include(u => u.Office)
                .FirstOrDefaultAsync(u => u.Username == f.Username.Trim());

            if (user is { LockedUntil: { } until } && until > now)
                return Results.LocalRedirect(Append(back, "login=locked"));

            var ok = user is { IsActive: true } && BCrypt.Net.BCrypt.Verify(f.Password, user.PasswordHash);
            db.LoginAttempts.Add(new LoginAttempt { Username = f.Username.Trim(), IpAddress = ip, WasSuccessful = ok });

            if (!ok)
            {
                if (user is not null && ++user.FailedLoginAttempts >= MaxFailures)
                {
                    user.LockedUntil = now + LockFor;
                    user.FailedLoginAttempts = 0;
                }
                await db.SaveChangesAsync();
                return Results.LocalRedirect(Append(back, "login=failed"));   // modal reopens with message
            }

            user!.FailedLoginAttempts = 0;
            user.LockedUntil = null;
            user.LastLoginAt = now;
            if (BCrypt.Net.BCrypt.PasswordNeedsRehash(user.PasswordHash, TargetBcryptCost))
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(f.Password, TargetBcryptCost);
            db.UserActivities.Add(new UserActivity { UserId = user.UserId, ActivityType = "LOGIN", Description = "Signed in", IpAddress = ip });
            await db.SaveChangesAsync();

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new(ClaimTypes.Name, user.DisplayName is { Length: > 0 } d ? d : user.FullName),
                new(ClaimTypes.Role, user.Role),
                new("office_id", user.OfficeId?.ToString() ?? ""),
                new("office_acronym", user.Office?.OfficeAcronym ?? ""),
                new("can_requisition", (user.Office?.CanRequisition ?? false) ? "1" : "0"),
                new("must_change_password", user.RequirePasswordChange ? "1" : "0"),
            };
            await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));

            // Resume what the guest was doing: catalog page reads these and calls AddToCartAsync.
            if (f.PendingItemId is int itemId)
                back = Append(back, $"add={itemId}&qty={Math.Max(1, f.PendingQty ?? 1)}");
            return Results.LocalRedirect(back);
        });

        g.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.LocalRedirect("/");
        });
    }

    private static string SafeLocal(string? url) =>
        !string.IsNullOrEmpty(url) && url.StartsWith('/') && !url.StartsWith("//") && !url.StartsWith("/\\") ? url : "/";

    private static string Append(string url, string query) =>
        url + (url.Contains('?') ? "&" : "?") + query;
}
