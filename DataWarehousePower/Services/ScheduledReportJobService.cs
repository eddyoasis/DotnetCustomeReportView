using DataWarehousePower.Models;
using DataWarehousePower.Repositories;
using Hangfire;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace DataWarehousePower.Services;

public sealed class ScheduledReportJobService(
    IScheduledReportJobRepository scheduledJobRepository,
    IReportRepository reportRepository,
    IColumnPreferenceRepository columnPreferenceRepository,
    IRecurringJobManager recurringJobManager,
    IHangfireDataProtectionService dataProtectionService,
    IOptions<HangfireOptions> hangfireOptions,
    ILogger<ScheduledReportJobService> logger) : IScheduledReportJobService
{
    public async Task<ScheduledJobListViewModel> GetListViewModelAsync()
    {
        List<ScheduledReportJob> entities = await scheduledJobRepository.GetAllAsync();

        ScheduledJobListViewModel viewModel = new()
        {
            Jobs = entities.Select(entity => new ScheduledJobListItemViewModel
            {
                Id = entity.Id,
                JobName = entity.JobName,
                HangfireJobId = entity.HangfireJobId,
                ReportDefinitionId = entity.ReportDefinitionId,
                ReportName = entity.ReportDefinition?.ReportName ?? $"Report #{entity.ReportDefinitionId}",
                Format = entity.Format,
                ClientCode = entity.ClientCode,
                FilterClientCode = entity.FilterClientCode,
                CronExpression = entity.CronExpression,
                ScheduleDisplay = BuildScheduleDisplay(entity.CronExpression),
                IsActive = entity.IsActive,
                CreatedUtc = entity.CreatedUtc
            }).ToList()
        };

        return viewModel;
    }

    public async Task<ScheduledJobFormViewModel> GetCreateFormAsync(string userId)
    {
        List<ReportDefinitionLookupItem> availableReports = await GetReportLookupAsync();
        Dictionary<int, List<string>> availableClientCodesByReportId = await GetClientCodesLookupAsync(userId, availableReports);

        return new ScheduledJobFormViewModel
        {
            IsActive = true,
            Format = "csv",
            ScheduleType = ScheduledJobFormViewModel.ScheduleTypeDailyTime,
            DailyTime = "08:30",
            EveryMinutes = 5,
            CronExpression = "0 8 * * *",
            AvailableReports = availableReports,
            AvailableClientCodesByReportId = availableClientCodesByReportId,
            AvailableClientCodes = []
        };
    }

    public async Task<ScheduledJobFormViewModel> GetEditFormAsync(int id, string userId)
    {
        ScheduledReportJob entity = await scheduledJobRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Scheduled job {id} was not found.");

        List<ReportDefinitionLookupItem> availableReports = await GetReportLookupAsync();
        Dictionary<int, List<string>> availableClientCodesByReportId = await GetClientCodesLookupAsync(userId, availableReports);

        ScheduledJobFormViewModel form = new()
        {
            Id = entity.Id,
            JobName = entity.JobName,
            ReportDefinitionId = entity.ReportDefinitionId,
            Format = entity.Format,
            CronExpression = entity.CronExpression,
            ClientCode = entity.ClientCode,
            FilterClientCode = entity.FilterClientCode,
            DateFrom = entity.DateFrom,
            DateTo = entity.DateTo,
            IsActive = entity.IsActive,
            AvailableReports = availableReports,
            AvailableClientCodesByReportId = availableClientCodesByReportId,
            AvailableClientCodes = BuildAvailableClientCodes(
                entity.ClientCode,
                availableClientCodesByReportId.TryGetValue(entity.ReportDefinitionId, out List<string>? reportClientCodes)
                    ? reportClientCodes
                    : [])
        };

        ApplyScheduleFromCron(form, entity.CronExpression);
        return form;
    }

    public async Task<int> CreateAsync(ScheduledJobFormViewModel form, string userId, string username)
    {
        ValidateForm(form);

        string hangfireJobId = form.JobName.Trim();

        ScheduledReportJob entity = new()
        {
            JobName = hangfireJobId,
            ReportDefinitionId = form.ReportDefinitionId,
            Format = form.Format.Trim().ToLowerInvariant(),
            CronExpression = BuildCronExpression(form),
            ClientCode = NormalizeNullable(form.ClientCode),
            FilterClientCode = NormalizeNullable(form.FilterClientCode),
            DateFrom = form.DateFrom,
            DateTo = form.DateTo,
            EncryptedPassword = dataProtectionService.Protect(form.Password),
            IsActive = form.IsActive,
            CreatedByUserId = userId,
            CreatedByUsername = username,
            CreatedUtc = DateTime.UtcNow,
            HangfireJobId = hangfireJobId
        };

        await scheduledJobRepository.AddAsync(entity);

        SyncRecurringJob(entity);
        return entity.Id;
    }

    public async Task UpdateAsync(ScheduledJobFormViewModel form, string userId, string username)
    {
        ValidateForm(form);

        ScheduledReportJob entity = await scheduledJobRepository.GetByIdForUpdateAsync(form.Id)
            ?? throw new InvalidOperationException($"Scheduled job {form.Id} was not found.");

        string newJobName = form.JobName.Trim();
        string oldHangfireJobId = entity.HangfireJobId;
        string newHangfireJobId = newJobName;

        entity.JobName = newJobName;
        entity.ReportDefinitionId = form.ReportDefinitionId;
        entity.Format = form.Format.Trim().ToLowerInvariant();
        entity.CronExpression = BuildCronExpression(form);
        entity.ClientCode = NormalizeNullable(form.ClientCode);
        entity.FilterClientCode = NormalizeNullable(form.FilterClientCode);
        entity.DateFrom = form.DateFrom;
        entity.DateTo = form.DateTo;
        entity.IsActive = form.IsActive;
        entity.UpdatedByUserId = userId;
        entity.UpdatedByUsername = username;
        entity.UpdatedUtc = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(form.Password))
        {
            entity.EncryptedPassword = dataProtectionService.Protect(form.Password);
        }

        if (!string.IsNullOrWhiteSpace(oldHangfireJobId) &&
            !string.Equals(oldHangfireJobId, newHangfireJobId, StringComparison.Ordinal))
        {
            recurringJobManager.RemoveIfExists(oldHangfireJobId);
        }

        entity.HangfireJobId = newHangfireJobId;

        await scheduledJobRepository.UpdateAsync(entity);
        SyncRecurringJob(entity);
    }

    public async Task DeleteAsync(int id)
    {
        ScheduledReportJob entity = await scheduledJobRepository.GetByIdForUpdateAsync(id)
            ?? throw new InvalidOperationException($"Scheduled job {id} was not found.");

        recurringJobManager.RemoveIfExists(entity.HangfireJobId);
        await scheduledJobRepository.DeleteAsync(entity);
    }

    public async Task SyncRecurringJobsAsync()
    {
        List<ScheduledReportJob> entities = await scheduledJobRepository.GetAllAsync();

        foreach (ScheduledReportJob entity in entities)
        {
            if (string.IsNullOrWhiteSpace(entity.HangfireJobId))
            {
                logger.LogWarning("Scheduled job {ScheduledJobId} has no Hangfire job id and was skipped.", entity.Id);
                continue;
            }

            SyncRecurringJob(entity);
        }
    }

    private void SyncRecurringJob(ScheduledReportJob entity)
    {
        if (!entity.IsActive)
        {
            recurringJobManager.RemoveIfExists(entity.HangfireJobId);
            return;
        }

        recurringJobManager.AddOrUpdate<IScheduledReportExecutionService>(
            entity.HangfireJobId,
            service => service.ExecuteAsync(entity.Id),
            entity.CronExpression,
            new RecurringJobOptions
            {
                TimeZone = HangfireTimeZoneResolver.Resolve(hangfireOptions.Value.TimeZoneId)
            });
    }

    private async Task<List<ReportDefinitionLookupItem>> GetReportLookupAsync()
    {
        List<ReportDefinition> reports = await reportRepository.GetAllReportsAsync();
        return reports
            .Select(report => new ReportDefinitionLookupItem { Id = report.Id, ReportName = report.ReportName })
            .ToList();
    }

    private async Task<Dictionary<int, List<string>>> GetClientCodesLookupAsync(
        string userId,
        IEnumerable<ReportDefinitionLookupItem> reports)
    {
        Dictionary<int, List<string>> clientCodesByReportId = [];

        foreach (ReportDefinitionLookupItem report in reports)
        {
            clientCodesByReportId[report.Id] = await columnPreferenceRepository.GetClientCodesAsync(userId, report.Id);
        }

        return clientCodesByReportId;
    }

    private static List<string> BuildAvailableClientCodes(string? currentClientCode, IEnumerable<string> savedClientCodes)
        => savedClientCodes
            .Append(currentClientCode ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();


    private static string? NormalizeNullable(string? value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static void ValidateForm(ScheduledJobFormViewModel form)
    {
        if (form.ReportDefinitionId <= 0)
        {
            throw new InvalidOperationException("Report is required.");
        }

        if (string.IsNullOrWhiteSpace(form.JobName))
        {
            throw new InvalidOperationException("Job name is required.");
        }

        string normalizedFormat = form.Format?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalizedFormat is not ("csv" or "excel" or "pdf"))
        {
            throw new InvalidOperationException("Format must be csv, excel, or pdf.");
        }

        string normalizedScheduleType = (form.ScheduleType ?? string.Empty).Trim().ToLowerInvariant();
        if (normalizedScheduleType is not (
            ScheduledJobFormViewModel.ScheduleTypeEveryMinutes or
            ScheduledJobFormViewModel.ScheduleTypeDailyTime or
            ScheduledJobFormViewModel.ScheduleTypeAdvancedCron))
        {
            throw new InvalidOperationException("Schedule type is invalid.");
        }

        _ = BuildCronExpression(form);

        if (form.Id == 0 && string.IsNullOrWhiteSpace(form.Password))
        {
            throw new InvalidOperationException("Password is required for a new scheduled job.");
        }

        if (form.DateFrom.HasValue && form.DateTo.HasValue && form.DateFrom.Value.Date > form.DateTo.Value.Date)
        {
            throw new InvalidOperationException("Date From cannot be later than Date To.");
        }
    }

    private static string BuildCronExpression(ScheduledJobFormViewModel form)
    {
        string scheduleType = (form.ScheduleType ?? string.Empty).Trim().ToLowerInvariant();

        if (scheduleType == ScheduledJobFormViewModel.ScheduleTypeEveryMinutes)
        {
            int interval = form.EveryMinutes ?? 0;
            if (interval is < 1 or > 1440)
            {
                throw new InvalidOperationException("Every minutes must be between 1 and 1440.");
            }

            return $"*/{interval} * * * *";
        }

        if (scheduleType == ScheduledJobFormViewModel.ScheduleTypeDailyTime)
        {
            string rawTime = form.DailyTime?.Trim() ?? string.Empty;
            if (!TimeOnly.TryParse(rawTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly time))
            {
                throw new InvalidOperationException("Daily time is invalid. Use HH:mm.");
            }

            return $"{time.Minute} {time.Hour} * * *";
        }

        string cronExpression = form.CronExpression?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            throw new InvalidOperationException("Cron expression is required.");
        }

        string[] segments = cronExpression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 5)
        {
            throw new InvalidOperationException("Cron expression must have at least 5 parts.");
        }

        return cronExpression;
    }

    private static void ApplyScheduleFromCron(ScheduledJobFormViewModel form, string cronExpression)
    {
        string[] segments = (cronExpression ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 5 &&
            segments[0].StartsWith("*/", StringComparison.Ordinal) &&
            int.TryParse(segments[0][2..], out int minutes) &&
            segments[1] == "*" &&
            segments[2] == "*" &&
            segments[3] == "*" &&
            segments[4] == "*")
        {
            form.ScheduleType = ScheduledJobFormViewModel.ScheduleTypeEveryMinutes;
            form.EveryMinutes = minutes;
            return;
        }

        if (segments.Length >= 5 &&
            int.TryParse(segments[0], out int minute) &&
            int.TryParse(segments[1], out int hour) &&
            segments[2] == "*" &&
            segments[3] == "*" &&
            segments[4] == "*" &&
            minute is >= 0 and < 60 &&
            hour is >= 0 and < 24)
        {
            form.ScheduleType = ScheduledJobFormViewModel.ScheduleTypeDailyTime;
            form.DailyTime = $"{hour:00}:{minute:00}";
            return;
        }

        form.ScheduleType = ScheduledJobFormViewModel.ScheduleTypeAdvancedCron;
    }

    private static string BuildScheduleDisplay(string? cronExpression)
    {
        string[] segments = (cronExpression ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length >= 5 &&
            segments[0].StartsWith("*/", StringComparison.Ordinal) &&
            int.TryParse(segments[0][2..], out int minutes) &&
            segments[1] == "*" &&
            segments[2] == "*" &&
            segments[3] == "*" &&
            segments[4] == "*")
        {
            return minutes == 1 ? "Every minute" : $"Every {minutes} minutes";
        }

        if (segments.Length >= 5 &&
            int.TryParse(segments[0], out int minute) &&
            int.TryParse(segments[1], out int hour) &&
            segments[2] == "*" &&
            segments[3] == "*" &&
            segments[4] == "*" &&
            minute is >= 0 and < 60 &&
            hour is >= 0 and < 24)
        {
            return $"Daily at {hour:00}:{minute:00}";
        }

        return "Advanced schedule";
    }
}
