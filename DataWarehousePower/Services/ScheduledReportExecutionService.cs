using DataWarehousePower.Helper;
using DataWarehousePower.Models;
using DataWarehousePower.Repositories;

namespace DataWarehousePower.Services;

public sealed class ScheduledReportExecutionService(
    IScheduledReportJobRepository scheduledJobRepository,
    IReportService reportService,
    IReportExportService reportExportService,
    IScheduledReportEmailService scheduledReportEmailService,
    IHangfireDataProtectionService dataProtectionService,
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
        (DateTime? effectiveDateFrom, DateTime? effectiveDateTo) = ResolveEffectiveDateRange(job);

        ReportViewModel? reportViewModel = await reportService.BuildReportViewModelAsync(
            job.ReportDefinitionId,
            job.CreatedByUserId,
            job.SchemaTemplate,
            job.FilterClientCode,
            effectiveDateFrom,
            effectiveDateTo);

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

        string baseDirectory = ResolveExportDirectory(job.ExportLocation);
        Directory.CreateDirectory(baseDirectory);

        var reportDate = effectiveDateFrom == effectiveDateTo ?
                $"{effectiveDateFrom:yyyy-MM-dd}" :
                $"{effectiveDateFrom:yyyy-MM-dd}_{effectiveDateTo:yyyy-MM-dd}";

        string safeReportName = string.Join("_", reportViewModel.ReportName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        string fileName = $"{safeReportName}_{job.FilterClientCode}_{reportDate}.zip";
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

            string emailSubject = $"{safeReportName} {job.FilterClientCode} {reportDate}";
            string emailBody = $"The {safeReportName} for {reportDate} was automatically exported for {job.FilterClientCode}, and the ZIP file is attached.";

            await scheduledReportEmailService.SendExportResultAsync(
                job.RecipientEmail,
                job.JobName,
                reportViewModel.ReportName,
                fileName,
                zipBytes,
                emailSubject,
                emailBody);
        }
    }

    private static string ResolveExportDirectory(string? exportLocation)
    {
        string normalizedExportLocation = exportLocation?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedExportLocation))
        {
            return AppContext.BaseDirectory;
        }

        if (Path.IsPathRooted(normalizedExportLocation))
        {
            return normalizedExportLocation;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, normalizedExportLocation));
    }

    private static (DateTime? DateFrom, DateTime? DateTo) ResolveEffectiveDateRange(ScheduledReportJob job)
    {
        if (job.IsCustom)
        {
            return (job.DateFrom?.Date, job.DateTo?.Date);
        }

        DateTime today = DateTimeHelper.GetCurrentLocalTime().Date;
        DateTime effectiveDate = today.DayOfWeek switch
        {
            DayOfWeek.Monday => today.AddDays(-3),
            DayOfWeek.Saturday => today.AddDays(-1),
            DayOfWeek.Sunday => today.AddDays(-2),
            _ => today.AddDays(-1)
        };

        return (effectiveDate, effectiveDate);
    }
}
