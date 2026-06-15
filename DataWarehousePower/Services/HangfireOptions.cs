namespace DataWarehousePower.Services;

public sealed class HangfireOptions
{
    public const string SectionName = "Hangfire";

    public string TimeZoneId { get; set; } = "Singapore Standard Time";

    public string DashboardPath { get; set; } = "/hangfire";

    public string AuditLogCleanupCron { get; set; } = "0 2 * * *";

    public int AuditLogRetentionDays { get; set; } = 90;

    public string ScheduledExportOutputDirectory { get; set; } = "ScheduledExports";
}
