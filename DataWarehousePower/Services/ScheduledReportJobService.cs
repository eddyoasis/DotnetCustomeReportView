using DataWarehousePower.Models;
using DataWarehousePower.Repositories;
using Hangfire;

namespace DataWarehousePower.Services;

public sealed class ScheduledReportJobService(
    IScheduledReportJobRepository scheduledJobRepository,
    IReportRepository reportRepository,
    IRecurringJobManager recurringJobManager,
    IHangfireDataProtectionService dataProtectionService,
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
                CronExpression = entity.CronExpression,
                IsActive = entity.IsActive,
                CreatedUtc = entity.CreatedUtc
            }).ToList()
        };

        return viewModel;
    }

    public async Task<ScheduledJobFormViewModel> GetCreateFormAsync()
    {
        return new ScheduledJobFormViewModel
        {
            IsActive = true,
            Format = "csv",
            CronExpression = "0 8 * * *",
            AvailableReports = await GetReportLookupAsync()
        };
    }

    public async Task<ScheduledJobFormViewModel> GetEditFormAsync(int id)
    {
        ScheduledReportJob entity = await scheduledJobRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Scheduled job {id} was not found.");

        return new ScheduledJobFormViewModel
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
            AvailableReports = await GetReportLookupAsync()
        };
    }

    public async Task<int> CreateAsync(ScheduledJobFormViewModel form, string userId, string username)
    {
        ValidateForm(form);

        ScheduledReportJob entity = new()
        {
            JobName = form.JobName.Trim(),
            ReportDefinitionId = form.ReportDefinitionId,
            Format = form.Format.Trim().ToLowerInvariant(),
            CronExpression = form.CronExpression.Trim(),
            ClientCode = NormalizeNullable(form.ClientCode),
            FilterClientCode = NormalizeNullable(form.FilterClientCode),
            DateFrom = form.DateFrom,
            DateTo = form.DateTo,
            EncryptedPassword = dataProtectionService.Protect(form.Password),
            IsActive = form.IsActive,
            CreatedByUserId = userId,
            CreatedByUsername = username,
            CreatedUtc = DateTime.UtcNow,
            HangfireJobId = string.Empty
        };

        await scheduledJobRepository.AddAsync(entity);

        entity.HangfireJobId = BuildHangfireJobId(entity.Id);
        await scheduledJobRepository.UpdateAsync(entity);

        SyncRecurringJob(entity);
        return entity.Id;
    }

    public async Task UpdateAsync(ScheduledJobFormViewModel form, string userId, string username)
    {
        ValidateForm(form);

        ScheduledReportJob entity = await scheduledJobRepository.GetByIdForUpdateAsync(form.Id)
            ?? throw new InvalidOperationException($"Scheduled job {form.Id} was not found.");

        entity.JobName = form.JobName.Trim();
        entity.ReportDefinitionId = form.ReportDefinitionId;
        entity.Format = form.Format.Trim().ToLowerInvariant();
        entity.CronExpression = form.CronExpression.Trim();
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

        if (string.IsNullOrWhiteSpace(entity.HangfireJobId))
        {
            entity.HangfireJobId = BuildHangfireJobId(entity.Id);
        }

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
            entity.CronExpression);
    }

    private async Task<List<ReportDefinitionLookupItem>> GetReportLookupAsync()
    {
        List<ReportDefinition> reports = await reportRepository.GetAllReportsAsync();
        return reports
            .Select(report => new ReportDefinitionLookupItem { Id = report.Id, ReportName = report.ReportName })
            .ToList();
    }

    private static string BuildHangfireJobId(int id)
    {
        return $"scheduled-export-{id}";
    }

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

        if (string.IsNullOrWhiteSpace(form.CronExpression))
        {
            throw new InvalidOperationException("Cron expression is required.");
        }

        if (form.Id == 0 && string.IsNullOrWhiteSpace(form.Password))
        {
            throw new InvalidOperationException("Password is required for a new scheduled job.");
        }

        if (form.DateFrom.HasValue && form.DateTo.HasValue && form.DateFrom.Value.Date > form.DateTo.Value.Date)
        {
            throw new InvalidOperationException("Date From cannot be later than Date To.");
        }
    }
}
