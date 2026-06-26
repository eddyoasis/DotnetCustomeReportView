using DataWarehousePower.Helper;
using DataWarehousePower.Models;
using DataWarehousePower.Repositories;
using System.Text.Json;

namespace DataWarehousePower.Services;

public sealed class ScheduledReportExecutionService(
    IScheduledReportJobRepository scheduledJobRepository,
    IReportService reportService,
    IReportExportService reportExportService,
    IScheduledReportEmailService scheduledReportEmailService,
    IHangfireDataProtectionService dataProtectionService,
    IConfiguration configuration,
    ILogger<ScheduledReportExecutionService> logger) : IScheduledReportExecutionService
{
    private const string LocalExportBasePathSection = "ScheduledJob:LocalExportBasePath";

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

        List<string> normalizedFormats = ParseJobFormats(job.Format);
        string password = dataProtectionService.Unprotect(job.EncryptedPassword);
        (DateTime? effectiveDateFrom, DateTime? effectiveDateTo) = ResolveEffectiveDateRange(job);
        Dictionary<string, string?> jobParameters = ParseJobParameters(job.Parameters);

        ReportViewModel? reportViewModel = await reportService.BuildReportViewModelAsync(
            reportId: job.ReportDefinitionId,
            userId: job.CreatedByUserId,
            schemaTemplate: job.SchemaTemplate,
            clientCode: job.ClientCode,
            dateFrom: effectiveDateFrom,
            dateTo: effectiveDateTo,
            parameterValues: jobParameters);

        if (reportViewModel is null)
        {
            logger.LogWarning(
                "Scheduled export job {ScheduledJobId} skipped because report {ReportDefinitionId} was not found.",
                scheduledJobId,
                job.ReportDefinitionId);
            return;
        }

        var reportDate = effectiveDateFrom == effectiveDateTo ?
                $"{effectiveDateFrom:yyyy-MM-dd}" :
                $"{effectiveDateFrom:yyyy-MM-dd}_{effectiveDateTo:yyyy-MM-dd}";

        string safeReportName = string.Join("_", reportViewModel.ReportName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        //string fileName = $"{safeReportName}_{job.ClientCode}_{reportDate}.zip";
        string fileName = $"{safeReportName}_{job.ClientCode}_{reportDate}_({DateTimeHelper.GetCurrentLocalTime():yyyy-MM-dd_HHmm}).zip";
        string zipSubFileName = $"{safeReportName}_format_{job.ClientCode}_{reportDate}_({DateTimeHelper.GetCurrentLocalTime():yyyy-MM-dd_HHmm})";

        byte[] zipBytes = await reportExportService.BuildPasswordProtectedZipAsync(
            reportViewModel,
            normalizedFormats,
            password,
            zipSubFileName);

        string primaryDirectory = ResolveExportDirectory(job.ExportLocation);
        string? localDirectory = ResolveLocalExportDirectory(job, configuration);
        List<string> targetDirectories = [primaryDirectory];

        if (!string.IsNullOrWhiteSpace(localDirectory) &&
            !targetDirectories.Contains(localDirectory, StringComparer.OrdinalIgnoreCase))
        {
            targetDirectories.Add(localDirectory);
        }

        foreach (string targetDirectory in targetDirectories)
        {
            Directory.CreateDirectory(targetDirectory);
            string fullPath = Path.Combine(targetDirectory, fileName);
            await File.WriteAllBytesAsync(fullPath, zipBytes);

            logger.LogInformation(
                "Scheduled export job {ScheduledJobId} produced file {ExportFilePath}.",
                scheduledJobId,
                fullPath);
        }

        string jobAction = (job.JobAction ?? string.Empty).Trim().ToLowerInvariant();
        if (jobAction == ScheduledJobActions.ExportFileAndEmailToUser)
        {
            if (string.IsNullOrWhiteSpace(job.RecipientEmail))
            {
                throw new InvalidOperationException($"Scheduled job {scheduledJobId} is configured for email action but recipient email is empty.");
            }

            string emailSubject = $"{safeReportName} {job.ClientCode} {reportDate}";
            string emailBody = $"The {safeReportName} for {reportDate} was automatically exported for {job.ClientCode}, and the ZIP file is attached.";

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

    private static string? ResolveLocalExportDirectory(ScheduledReportJob job, IConfiguration configuration)
    {
        if (!job.ExportToLocalFolder)
        {
            return null;
        }

        string subfolder = ExtractExportSubfolder(job.ExportLocation);
        if (string.IsNullOrWhiteSpace(subfolder))
        {
            return null;
        }

        string configuredBasePath = configuration[LocalExportBasePathSection] ?? "%temp%";
        string expandedBasePath = Environment.ExpandEnvironmentVariables(configuredBasePath).Trim();
        if (string.IsNullOrWhiteSpace(expandedBasePath))
        {
            expandedBasePath = Path.GetTempPath();
        }

        if (!Path.IsPathRooted(expandedBasePath))
        {
            expandedBasePath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), expandedBasePath));
        }

        return Path.Combine(expandedBasePath, subfolder);
    }

    private static string ExtractExportSubfolder(string? exportLocation)
    {
        string normalizedExportLocation = (exportLocation ?? string.Empty).Trim().TrimEnd('\\', '/');
        if (string.IsNullOrWhiteSpace(normalizedExportLocation))
        {
            return string.Empty;
        }

        string? folderName = Path.GetFileName(normalizedExportLocation);
        return folderName?.Trim() ?? string.Empty;
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

    private static List<string> ParseJobFormats(string? value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return ["csv"];
        }

        List<string> parsedFormats = normalized
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(format => format.Trim().ToLowerInvariant())
            .Where(format => format is "csv" or "excel" or "pdf")
            .Distinct()
            .ToList();

        return parsedFormats.Count > 0 ? parsedFormats : ["csv"];
    }

    private static Dictionary<string, string?> ParseJobParameters(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            List<ScheduledJobParameterValue> parameters = JsonSerializer.Deserialize<List<ScheduledJobParameterValue>>(json)
                ?? [];

            return parameters
                .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Name))
                .GroupBy(parameter => NormalizeParameterName(parameter.Name), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.First().Value,
                    StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string NormalizeParameterName(string? name)
        => (name ?? string.Empty).Trim().TrimStart('@');

    private sealed class ScheduledJobParameterValue
    {
        public string Name { get; set; } = string.Empty;
        public string? Value { get; set; }
    }
}
