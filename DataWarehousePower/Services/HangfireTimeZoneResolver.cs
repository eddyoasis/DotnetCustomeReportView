namespace DataWarehousePower.Services;

public static class HangfireTimeZoneResolver
{
    private static readonly string[] UtcPlusEightFallbackIds =
    [
        "Singapore Standard Time",
        "W. Australia Standard Time",
        "Asia/Singapore",
        "Asia/Kuala_Lumpur",
        "Australia/Perth"
    ];

    public static TimeZoneInfo Resolve(string? configuredTimeZoneId)
    {
        if (!string.IsNullOrWhiteSpace(configuredTimeZoneId))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(configuredTimeZoneId.Trim());
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        foreach (string timeZoneId in UtcPlusEightFallbackIds)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone(
            id: "UTC+08",
            baseUtcOffset: TimeSpan.FromHours(8),
            displayName: "UTC+08",
            standardDisplayName: "UTC+08");
    }
}
