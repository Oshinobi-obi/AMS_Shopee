// AccountEndpoints.cs — cookie login/logout.
// An interactive (SignalR) component cannot set a cookie, so the login modal
// renders a normal <form method="post" action="/account/login"> containing
// <AntiforgeryToken />, posts here, and the page reloads signed in.
// Wired up in Program.cs with app.MapAccountEndpoints().

using System.Security.Claims;
using AMS_Shopee.Data;
using AMS_Shopee.Data.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AMS_Shopee.Endpoints;

public static class AccountEndpoints
{
    private const uint MaxFailures = 5;
    private static readonly TimeSpan LockFor = TimeSpan.FromMinutes(15);
    private const int TargetBcryptCost = 12;
    // Same convention as RequisitionService: all DB timestamps are Philippine time.
    private static readonly TimeZoneInfo Manila = AMS_Shopee.Services.PhZone.Manila;

    public sealed class LoginForm
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string? ReturnUrl { get; set; }
        public uint? PendingItemId { get; set; }   // item the guest tried to add
        public int? PendingQty { get; set; }
    }

    public static void MapAccountEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/account");

        group.MapPost("/login", async ([FromForm] LoginForm f, HttpContext http,
                                       IDbContextFactory<AmsDbContext> dbFactory, TimeProvider clock) =>
        {
            var back = SafeLocal(f.ReturnUrl);
            var username = f.Username.Trim();
            await using var db = await dbFactory.CreateDbContextAsync();
            var now = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Manila).DateTime;
            var ip = http.Connection.RemoteIpAddress?.ToString();

            var user = await db.Users.Include(u => u.Office)
                .FirstOrDefaultAsync(u => u.Username == username);

            if (user?.LockedUntil is { } until && until > now)
                return Results.LocalRedirect(Append(back, "login=locked"));

            var ok = user is not null && user.IsActive != false && PasswordMatches(f.Password, user.PasswordHash);
            db.LoginAttempts.Add(new LoginAttempt { Username = username, IpAddress = ip, WasSuccessful = ok });

            if (!ok)
            {
                if (user is not null && ++user.FailedLoginAttempts >= MaxFailures)
                {
                    user.LockedUntil = now + LockFor;
                    user.FailedLoginAttempts = 0;
                }
                await db.SaveChangesAsync();
                return Results.LocalRedirect(Append(back, "login=failed"));   // modal reopens with a message
            }

            user!.FailedLoginAttempts = 0;
            user.LockedUntil = null;
            user.LastLoginAt = now;
            if (BCrypt.Net.BCrypt.PasswordNeedsRehash(user.PasswordHash, TargetBcryptCost))
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(f.Password, TargetBcryptCost);
            db.UserActivities.Add(new UserActivity
            {
                UserId = user.UserId, ActivityType = "LOGIN", Description = "Signed in", IpAddress = ip,
            });
            await db.SaveChangesAsync();

            await SignInAsync(http, user);

            // Resume what the guest was doing: the catalog page reads ?add=&qty= and adds the item.
            if (f.PendingItemId is uint itemId)
                back = Append(back, $"add={itemId}&qty={Math.Max(1, f.PendingQty ?? 1)}");
            return Results.LocalRedirect(back);
        });

        // Re-issues the cookie from the database (after a password change).
        group.MapGet("/refresh", async (HttpContext http, IDbContextFactory<AmsDbContext> dbFactory, string? returnUrl) =>
        {
            if (!uint.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
                return Results.LocalRedirect("/");
            await using var db = await dbFactory.CreateDbContextAsync();
            var user = await db.Users.Include(u => u.Office).AsNoTracking().FirstOrDefaultAsync(u => u.UserId == id);
            if (user is null || user.IsActive == false)
            {
                await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return Results.LocalRedirect("/");
            }
            await SignInAsync(http, user);
            return Results.LocalRedirect(SafeLocal(returnUrl));
        }).RequireAuthorization();

        group.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.LocalRedirect("/");
        });
    }

    private static Task SignInAsync(HttpContext http, User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, string.IsNullOrWhiteSpace(user.DisplayName) ? user.FullName : user.DisplayName),
            new(ClaimTypes.Role, user.Role),
            new("office_id", user.OfficeId?.ToString() ?? ""),
            new("office_acronym", user.Office?.OfficeAcronym ?? ""),
            new("can_requisition", user.Office?.CanRequisition == true ? "1" : "0"),
            new("must_change_password", user.RequirePasswordChange ? "1" : "0"),
        };
        return http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
    }

    private static bool PasswordMatches(string password, string hash)
    {
        try { return BCrypt.Net.BCrypt.Verify(password, hash); }
        catch (BCrypt.Net.SaltParseException) { return false; }   // malformed hash in DB
    }

    private static string SafeLocal(string? url) =>
        !string.IsNullOrEmpty(url) && url.StartsWith('/') && !url.StartsWith("//") && !url.StartsWith("/\\")
            ? url : "/";

    private static string Append(string url, string query) =>
        url + (url.Contains('?') ? "&" : "?") + query;
}
