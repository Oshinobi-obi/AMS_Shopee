namespace AMS_Shopee.Services;

/// Philippine time zone that works on any server. Some Windows hosts don't know
/// the "Asia/Manila" name, so fall back to the Windows name, then to a fixed UTC+8.
public static class PhZone
{
    public static readonly TimeZoneInfo Manila = Find();

    private static TimeZoneInfo Find()
    {
        foreach (var id in new[] { "Asia/Manila", "Singapore Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("PHT", TimeSpan.FromHours(8), "Philippine Time", "Philippine Time");
    }
}
