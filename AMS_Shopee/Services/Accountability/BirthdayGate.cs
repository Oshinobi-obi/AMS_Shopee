// This is the birthday check of My accountability. The page is confidential, so before
// a person's documents are shown they have to enter their birthday.
//
//   First visit: the person sets their birthday once. I only save a hash of it (BirthdayHash)
//   in personnel_birthdays (database update 019).
//   Next visits: the birthday has to match. After 5 wrong tries the person is locked out
//   for 15 minutes.
//   To change a birthday or unlock early, AMS uses e-PINS > Employees.
//
// When the birthday matches, the page gets a short pass (a protected token) that the PDF links
// use, so nobody can open a document just by guessing its address.

using AMS_Shopee.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace AMS_Shopee.Services.Accountability;

public enum BirthdayCheck { Ok, Wrong, Locked, NotSet, Unavailable }

public sealed record BirthdayStatus(bool IsSet, DateTime? LockedUntilUtc, bool Unavailable = false)
{
    public bool IsLocked => LockedUntilUtc is { } u && u > DateTime.UtcNow;
}

public sealed record BirthdayResult(BirthdayCheck Check, int TriesLeft = 0, DateTime? LockedUntilUtc = null);

public sealed class BirthdayGate(IDbContextFactory<AmsDbContext> dbFactory, IDataProtectionProvider protection, ILogger<BirthdayGate> log)
{
    public const int MaxTries = 5;
    public static readonly TimeSpan LockFor = TimeSpan.FromMinutes(15);
    /// How long a pass (and the opened page) stays valid.
    public static readonly TimeSpan PassLifetime = TimeSpan.FromMinutes(30);

    private readonly ITimeLimitedDataProtector _pass =
        protection.CreateProtector("AMS_Shopee.Accountability.Pass.v1").ToTimeLimitedDataProtector();

    public async Task<BirthdayStatus> StatusAsync(uint personnelId)
    {
        try
        {
            var row = await ReadAsync(personnelId);
            return new BirthdayStatus(row is not null, row?.LockedUntil);
        }
        catch (Exception ex) when (MissingTable(ex))
        {
            log.LogError("personnel_birthdays table is missing. Run database update 019_personnel_birthdays.sql.");
            return new BirthdayStatus(false, null, Unavailable: true);
        }
    }

    /// Saves the birthday the first time. Returns false if someone already set it a moment ago.
    public async Task<bool> SetFirstTimeAsync(uint personnelId, DateOnly birthday)
    {
        var hash = BirthdayHash.Hash(personnelId, birthday);
        await using var db = await dbFactory.CreateDbContextAsync();
        var added = await db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT IGNORE INTO personnel_birthdays (personnel_id, birthday_hash, set_by, set_at, last_verified)
            VALUES ({personnelId}, {hash}, 'self', {DateTime.UtcNow}, {DateTime.UtcNow})");
        if (added == 1) log.LogInformation("Birthday set for the first time by personnel {Id}", personnelId);
        return added == 1;
    }

    public async Task<BirthdayResult> VerifyAsync(uint personnelId, DateOnly birthday)
    {
        BirthdayRow? row;
        try { row = await ReadAsync(personnelId); }
        catch (Exception ex) when (MissingTable(ex)) { return new BirthdayResult(BirthdayCheck.Unavailable); }
        if (row is null) return new BirthdayResult(BirthdayCheck.NotSet);
        if (row.LockedUntil is { } until && until > DateTime.UtcNow) return new BirthdayResult(BirthdayCheck.Locked, 0, until);

        await using var db = await dbFactory.CreateDbContextAsync();
        if (BirthdayHash.Verify(personnelId, birthday, row.BirthdayHash))
        {
            await db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE personnel_birthdays SET failed_tries = 0, locked_until = NULL, last_verified = {DateTime.UtcNow}
                 WHERE personnel_id = {personnelId}");
            return new BirthdayResult(BirthdayCheck.Ok);
        }

        // If the lock already ran out, the count starts over
        var tries = (row.LockedUntil is not null ? 0 : row.FailedTries) + 1;
        DateTime? lockUntil = tries >= MaxTries ? DateTime.UtcNow.Add(LockFor) : null;
        await db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE personnel_birthdays SET failed_tries = {(lockUntil is null ? tries : 0)}, locked_until = {lockUntil}
             WHERE personnel_id = {personnelId}");
        if (lockUntil is not null)
        {
            log.LogWarning("My accountability of personnel {Id} locked after {Tries} wrong birthdays", personnelId, MaxTries);
            return new BirthdayResult(BirthdayCheck.Locked, 0, lockUntil);
        }
        return new BirthdayResult(BirthdayCheck.Wrong, MaxTries - tries);
    }

    // Pass for the PDF links

    public string IssuePass(uint personnelId) => _pass.Protect(personnelId.ToString(), PassLifetime);

    public uint? ReadPass(string? pass)
    {
        if (string.IsNullOrWhiteSpace(pass)) return null;
        try { return uint.TryParse(_pass.Unprotect(pass), out var id) ? id : null; }
        catch (System.Security.Cryptography.CryptographicException) { return null; }   // expired or edited
    }

    private async Task<BirthdayRow?> ReadAsync(uint personnelId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Database.SqlQuery<BirthdayRow>($@"
            SELECT birthday_hash AS BirthdayHash, failed_tries AS FailedTries, locked_until AS LockedUntil
              FROM personnel_birthdays WHERE personnel_id = {personnelId}").FirstOrDefaultAsync();
    }

    private static bool MissingTable(Exception ex) => ex.GetBaseException().Message.Contains("personnel_birthdays")
                                                     && ex.GetBaseException().Message.Contains("doesn't exist");

    private sealed class BirthdayRow
    {
        public string BirthdayHash { get; set; } = "";
        public int FailedTries { get; set; }
        public DateTime? LockedUntil { get; set; }
    }
}
