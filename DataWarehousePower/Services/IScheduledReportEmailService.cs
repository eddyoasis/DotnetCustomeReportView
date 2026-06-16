namespace DataWarehousePower.Services;

public interface IScheduledReportEmailService
{
    Task SendExportResultAsync(
        string recipientEmail,
        string jobName,
        string reportName,
        string fileName,
        byte[] zipBytes,
        CancellationToken cancellationToken = default);
}
