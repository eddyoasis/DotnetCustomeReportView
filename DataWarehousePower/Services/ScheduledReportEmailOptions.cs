namespace DataWarehousePower.Services;

public sealed class ScheduledReportEmailOptions
{
    public const string SectionName = "ScheduledReportEmail";

    public bool Enabled { get; set; }
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 25;
    public bool UseSsl { get; set; } = true;
    public string SenderEmail { get; set; } = string.Empty;
    public string SenderDisplayName { get; set; } = "DataWarehousePower";
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string SubjectPrefix { get; set; } = "[DataWarehousePower]";
}
