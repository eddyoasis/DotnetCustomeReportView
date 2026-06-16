using DataWarehousePower.Models;
using DataWarehousePower.Repositories;
using Microsoft.Extensions.Options;

namespace DataWarehousePower.Services;

public sealed class ScheduledReportExecutionService(
    IScheduledReportJobRepository scheduledJobRepository,
    IReportService reportService,
    IReportExportService reportExportService,
    IScheduledReportEmailService scheduledReportEmailService,
    IHangfireDataProtectionService dataProtectionService,
    IOptions<HangfireOptions> options,
    ILogger<ScheduledReportExecutionService> logger) : IScheduledReportExecutionService
{
    public async Task ExecuteAsync(int scheduledJobId)
    {
        ScheduledReportJob? job = await scheduledJobRepository.GetByIdAsync(scheduledJobId);
        if (job is null)
        {
            logger.LogWarning("Scheduled export job {ScheduledJobId} not found.", scheduledJobId);
            return;
        }

        if (!job.IsActive)
        {
            logger.LogInformation("Scheduled export job {ScheduledJobId} is disabled.", scheduledJobId);
            return;
        }

        string normalizedFormat = job.Format.Trim().ToLowerInvariant();
        string password = dataProtectionService.Unprotect(job.EncryptedPassword);

        ReportViewModel? reportViewModel = await reportService.BuildReportViewModelAsync(
            job.ReportDefinitionId,
            job.CreatedByUserId,
            job.ClientCode,
            job.FilterClientCode,
            job.DateFrom,
            job.DateTo);

        if (reportViewModel is null)
        {
            logger.LogWarning(
                "Scheduled export job {ScheduledJobId} skipped because report {ReportDefinitionId} was not found.",
                scheduledJobId,
                job.ReportDefinitionId);
            return;
        }

        byte[] zipBytes = await reportExportService.BuildPasswordProtectedZipAsync(
            reportViewModel,
            normalizedFormat,
            password);

        string directory = options.Value.ScheduledExportOutputDirectory;
        string baseDirectory = Path.IsPathRooted(directory)
            ? directory
            : Path.Combine(AppContext.BaseDirectory, directory);
        Directory.CreateDirectory(baseDirectory);

        string safeReportName = string.Join("_", reportViewModel.ReportName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        string fileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{safeReportName}_{job.Id}.zip";
        string fullPath = Path.Combine(baseDirectory, fileName);

        await File.WriteAllBytesAsync(fullPath, zipBytes);

        logger.LogInformation(
            "Scheduled export job {ScheduledJobId} produced file {ExportFilePath}.",
            scheduledJobId,
            fullPath);

        string jobAction = (job.JobAction ?? string.Empty).Trim().ToLowerInvariant();
        if (jobAction == ScheduledJobActions.ExportFileAndEmailToUser)
        {
            if (string.IsNullOrWhiteSpace(job.RecipientEmail))
            {
                throw new InvalidOperationException($"Scheduled job {scheduledJobId} is configured for email action but recipient email is empty.");
            }

            await scheduledReportEmailService.SendExportResultAsync(
                job.RecipientEmail,
                job.JobName,
                reportViewModel.ReportName,
                fileName,
                zipBytes);
        }
    }
}
