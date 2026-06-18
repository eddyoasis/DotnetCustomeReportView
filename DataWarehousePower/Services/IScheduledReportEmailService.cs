namespace DataWarehousePower.Services;

public interface IScheduledReportEmailService
{
    Task SendExportResultAsync(
        string recipientEmail,
        string jobName,
        string reportName,
        string fileName,
        byte[] zipBytes,
        string emailSubject,
        string emailBody,
        CancellationToken cancellationToken = default);
}
