using DataWarehousePower.Models;
using DataWarehousePower.Repositories;
using Hangfire;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net.Mail;
using System.Text;
using System.Text.Json;

namespace DataWarehousePower.Services;

public sealed class ScheduledReportJobService(
    IScheduledReportJobRepository scheduledJobRepository,
    IReportRepository reportRepository,
    IDataFileManageRepository dataFileManageRepository,
    IDataFileManageService dataFileManageService,
    IColumnPreferenceRepository columnPreferenceRepository,
    IRecurringJobManager recurringJobManager,
    IHangfireDataProtectionService dataProtectionService,
    IOptions<HangfireOptions> hangfireOptions,
    ILogger<ScheduledReportJobService> logger) : IScheduledReportJobService
{
    private static readonly string[] SupportedExportFormats = ["csv", "excel", "pdf"];
    private static readonly string[] SupportedAutoDateIntervalUnits =
    [
        ScheduledJobFormViewModel.AutoDateIntervalMinutely,
        ScheduledJobFormViewModel.AutoDateIntervalHourly,
        ScheduledJobFormViewModel.AutoDateIntervalDaily,
        ScheduledJobFormViewModel.AutoDateIntervalWeekly,
        ScheduledJobFormViewModel.AutoDateIntervalMonthly,
        ScheduledJobFormViewModel.AutoDateIntervalYearly,
        ScheduledJobFormViewModel.AutoDateIntervalLastDay,
        ScheduledJobFormViewModel.AutoDateIntervalLastMonth,
        ScheduledJobFormViewModel.AutoDateIntervalLastYear
    ];
    private static readonly int[] OrderedCronWeekdays = [1, 2, 3, 4, 5, 6, 0];
    private static readonly Dictionary<int, string> WeekdayDisplayNames = new()
    {
        [1] = "Monday",
        [2] = "Tuesday",
        [3] = "Wednesday",
        [4] = "Thursday",
        [5] = "Friday",
        [6] = "Saturday",
        [0] = "Sunday"
    };
    private static readonly HashSet<string> ReservedParameterNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "ClientCode",
        "DateFrom",
        "DateTo"
    };

    public async Task<ScheduledJobListViewModel> GetListViewModelAsync(string userId, ScheduledJobFilterViewModel? filter = null)
    {
        List<ScheduledReportJob> entities = await scheduledJobRepository.GetAllByUserIdAsync(userId);

        List<ScheduledJobListItemViewModel> allJobs = entities.Select(entity => new ScheduledJobListItemViewModel
        {
            Id = entity.Id,
            JobName = entity.JobName,
            HangfireJobId = entity.HangfireJobId,
            SourceType = entity.ReportDefinitionId.HasValue
                ? "Report"
                : entity.DataFileDefinitionId.HasValue
                    ? "DataFile"
                    : "Unknown",
            ReportDefinitionId = entity.ReportDefinitionId ?? entity.DataFileDefinitionId ?? 0,
            ReportName = entity.ReportDefinition?.ReportName
                ?? entity.DataFileDefinition?.DataFileName
                ?? $"Source #{entity.ReportDefinitionId ?? entity.DataFileDefinitionId}",
            Format = string.Join(", ", ParseFormats(entity.Format).Select(format => format.ToUpperInvariant())),
            JobAction = entity.JobAction,
            RecipientEmail = entity.RecipientEmail,
            ExportLocation = entity.ExportLocation,
            ExportToLocalFolder = entity.ExportToLocalFolder,
            SchemaTemplate = entity.SchemaTemplate,
            ClientCode = entity.ClientCode,
            CronExpression = entity.CronExpression,
            ScheduleDisplay = BuildScheduleDisplay(entity.CronExpression),
            IsActive = entity.IsActive,
            CreatedUtc = entity.CreatedUtc
        }).ToList();

        List<string> availableFormats = entities
            .SelectMany(entity => ParseFormats(entity.Format))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(format => format)
            .ToList();
        List<string> availableJobActions = ScheduledJobActions.All.ToList();
        List<string> availableSchemaTemplates = allJobs
            .Select(j => j.SchemaTemplate)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .Order()
            .Select(c => c!)
            .ToList();

        List<string> availableClientCodes = allJobs
            .Select(j => j.ClientCode)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .Order()
            .Select(c => c!)
            .ToList();

        IEnumerable<ScheduledJobListItemViewModel> filtered = allJobs;

        if (filter is not null)
        {
            if (!string.IsNullOrWhiteSpace(filter.SourceType))
                filtered = filtered.Where(j => string.Equals(j.SourceType, filter.SourceType.Trim(), StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(filter.JobName))
                filtered = filtered.Where(j => j.JobName.Contains(filter.JobName, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(filter.ReportName))
                filtered = filtered.Where(j => j.ReportName.Contains(filter.ReportName, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(filter.Format))
                filtered = filtered.Where(j => ParseFormats(j.Format).Contains(filter.Format.Trim(), StringComparer.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(filter.JobAction))
                filtered = filtered.Where(j => j.JobAction.Equals(filter.JobAction, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(filter.SchemaTemplate))
                filtered = filtered.Where(j => string.Equals(j.SchemaTemplate, filter.SchemaTemplate, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(filter.ClientCode))
                filtered = filtered.Where(j => string.Equals(j.ClientCode, filter.ClientCode, StringComparison.OrdinalIgnoreCase));

            if (filter.IsActive.HasValue)
                filtered = filtered.Where(j => j.IsActive == filter.IsActive.Value);
        }

        ScheduledJobListViewModel viewModel = new()
        {
            Jobs = filtered.ToList(),
            Filter = filter ?? new ScheduledJobFilterViewModel(),
            AvailableFormats = availableFormats,
            AvailableJobActions = availableJobActions,
            AvailableSchemaTemplates = availableSchemaTemplates,
            AvailableClientCodes = availableClientCodes
        };

        return viewModel;
    }

    public async Task<int?> FindExistingJobIdAsync(string userId, int reportDefinitionId, string? schemaTemplate, string? clientCode)
    {
        List<ScheduledReportJob> entities = await scheduledJobRepository.GetAllByUserIdAsync(userId);
        string? normalizedSchemaTemplate = NormalizeNullable(schemaTemplate);
        string? normalizedClientCode = NormalizeNullable(clientCode);

        ScheduledReportJob? existingJob = entities.FirstOrDefault(entity =>
            (entity.ReportDefinitionId == reportDefinitionId || entity.DataFileDefinitionId == reportDefinitionId)
            && string.Equals(NormalizeNullable(entity.SchemaTemplate), normalizedSchemaTemplate, StringComparison.OrdinalIgnoreCase)
            && string.Equals(NormalizeNullable(entity.ClientCode), normalizedClientCode, StringComparison.OrdinalIgnoreCase));

        return existingJob?.Id;
    }

    public async Task<ScheduledJobFormViewModel> GetCreateFormAsync(string userId, string? userDepartment = null)
    {
        List<string> availableClientCodes = await reportRepository.GetClientCodesByUserIdAsync(userId);
        List<string> availableClientCodeFolders = await reportRepository.GetClientCodeFoldersByUserIdAsync(userId);

        List<ReportDefinitionLookupItem> availableReports = new List<ReportDefinitionLookupItem>();
        Dictionary<int, List<string>> availableSchemaTemplatesByReportId = new Dictionary<int, List<string>>();

        var dataFiles = await GetDataFileLookupAsync(userId, userDepartment);
        availableReports.AddRange(dataFiles);
        var dataFileSchemaTemplates = await GetDataFileSchemaTemplatesLookupAsync(userId, availableReports);

        var reports = await GetReportLookupAsync(userDepartment);
        availableReports.AddRange(reports);
        var reportSchemaTemplates = await GetClientCodesLookupAsync(userId, availableReports);

        availableSchemaTemplatesByReportId =
            availableSchemaTemplatesByReportId
            .Concat(dataFileSchemaTemplates)
            .Concat(reportSchemaTemplates)
            .GroupBy(kvp => kvp.Key)
            .ToDictionary(g => g.Key, g => g.First().Value);

        //availableSchemaTemplatesByReportId =
        //    availableSchemaTemplatesByReportId
        //    .Concat(dataFileSchemaTemplates)
        //    .Concat(reportSchemaTemplates)
        //    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        Dictionary<int, List<ScheduledJobParameterInputViewModel>> availableParametersByReportId =
            await BuildParameterLookupByReportIdAsync(availableReports, userDepartment);

        return new ScheduledJobFormViewModel
        {
            IsActive = true,
            IsCustom = false,
            AutoDateIntervalUnit = ScheduledJobFormViewModel.AutoDateIntervalDaily,
            AutoDateIntervalValue = 1,
            Formats = ["csv"],
            JobAction = ScheduledJobActions.ExportFile,
            ScheduleType = ScheduledJobFormViewModel.ScheduleTypeDailyTime,
            DailyTime = "08:30",
            CustomDaysTime = "08:30",
            SelectedWeekdays = [1],
            EveryMinutes = 5,
            CronExpression = "0 8 * * *",
            ExportLocation = null,
            ExportToLocalFolder = false,
            //AvailableReports = availableReports,
            AvailableReports = reports,
            AvailableClientCodes = availableClientCodes,
            AvailableClientCodeFolders = availableClientCodeFolders,
            AvailableSchemaTemplatesByReportId = availableSchemaTemplatesByReportId,
            AvailableSchemaTemplates = [],
            AvailableParametersByReportId = availableParametersByReportId
        };
    }

    public async Task<ScheduledJobFormViewModel> GetEditFormAsync(int id, string userId, string? userDepartment = null)
    {
        ScheduledReportJob entity = await scheduledJobRepository.GetByIdForUserAsync(id, userId)
            ?? throw new InvalidOperationException($"Scheduled job {id} was not found.");

        List<ReportDefinitionLookupItem> availableReports = await GetReportLookupAsync(userDepartment);
        List<string> availableClientCodes = await reportRepository.GetClientCodesByUserIdAsync(userId);
        List<string> availableClientCodeFolders = await reportRepository.GetClientCodeFoldersByUserIdAsync(userId);
        Dictionary<int, List<string>> availableSchemaTemplatesByReportId = await GetClientCodesLookupAsync(userId, availableReports);
        Dictionary<int, List<ScheduledJobParameterInputViewModel>> availableParametersByReportId =
            await BuildParameterLookupByReportIdAsync(availableReports, userDepartment);

        ScheduledJobFormViewModel form = new()
        {
            Id = entity.Id,
            JobName = entity.JobName,
            ReportDefinitionId = entity.ReportDefinitionId ?? entity.DataFileDefinitionId ?? 0,
            IsReport = entity.ReportDefinitionId > 0,
            Formats = ParseFormats(entity.Format),
            JobAction = entity.JobAction,
            RecipientEmail = entity.RecipientEmail,
            IsExportToClientFolder = entity.IsExportToClientFolder,
            ExportLocation = entity.ExportLocation,
            ExportLocationSubfolder = ExtractExportLocationSubfolder(entity.ExportLocation),
            ExportToLocalFolder = entity.ExportToLocalFolder,
            CronExpression = entity.CronExpression,
            SchemaTemplate = entity.SchemaTemplate,
            ClientCode = entity.ClientCode,
            Parameters = entity.Parameters,
            DateFrom = entity.DateFrom,
            DateTo = entity.DateTo,
            IsCustom = entity.IsCustom,
            AutoDateIntervalUnit = NormalizeAutoDateIntervalUnit(entity.AutoDateIntervalUnit) ?? ScheduledJobFormViewModel.AutoDateIntervalDaily,
            AutoDateIntervalValue = entity.AutoDateIntervalValue is > 0 ? entity.AutoDateIntervalValue : 1,
            ExistingPassword = dataProtectionService.Unprotect(entity.EncryptedPassword),
            IsActive = entity.IsActive,
            RequiresSchemaTemplateAndClientCode =
                !string.IsNullOrWhiteSpace(entity.SchemaTemplate)
                || !string.IsNullOrWhiteSpace(entity.ClientCode),
            AvailableReports = availableReports,
            AvailableClientCodes = BuildAvailableClientCodes(entity.ClientCode, availableClientCodes),
            AvailableClientCodeFolders = availableClientCodeFolders,
            AvailableSchemaTemplatesByReportId = availableSchemaTemplatesByReportId,
            AvailableParametersByReportId = availableParametersByReportId,
            AvailableSchemaTemplates = BuildAvailableSchemaTemplates(
                entity.SchemaTemplate,
                availableSchemaTemplatesByReportId.TryGetValue(entity.ReportDefinitionId ?? 0, out List<string>? reportClientCodes)
                    ? reportClientCodes
                    : [])
        };

        ApplyScheduleFromCron(form, entity.CronExpression);
        return form;
    }

    public async Task<int> CreateAsync(ScheduledJobFormViewModel form, string userId, string username, string userDepartment)
    {
        ValidateForm(form);
        string sourceName;
        string? normalizedParameters;

        if (form.RequiresSchemaTemplateAndClientCode)
        {
            ReportDefinition report = await GetReportDefinitionAsync(form.ReportDefinitionId, userDepartment);
            sourceName = report.ReportName;
            normalizedParameters = await NormalizeScheduledParametersJsonAsync(form.Parameters, report);
        }
        else
        {
            sourceName = await GetDataFileNameAsync(form.ReportDefinitionId, userId, userDepartment);
            normalizedParameters = null;
        }

        string generatedJobName = await BuildJobNameAsync(form, userId, userDepartment, null, sourceName);

        ScheduledReportJob entity = new()
        {
            JobName = generatedJobName,
            ReportDefinitionId = form.RequiresSchemaTemplateAndClientCode ? form.ReportDefinitionId : null,
            DataFileDefinitionId = form.RequiresSchemaTemplateAndClientCode ? null : form.ReportDefinitionId,
            Format = BuildFormatsStorageValue(form.Formats),
            JobAction = NormalizeJobAction(form.JobAction),
            RecipientEmail = NormalizeRecipientEmails(form.RecipientEmail),
            CronExpression = BuildCronExpression(form),
            SchemaTemplate = NormalizeNullable(form.SchemaTemplate),
            ClientCode = NormalizeNullable(form.ClientCode),
            Parameters = normalizedParameters,
            IsExportToClientFolder = form.IsExportToClientFolder,
            ExportLocation = NormalizeNullable(form.ExportLocation),
            ExportToLocalFolder = form.ExportToLocalFolder,
            DateFrom = form.IsCustom ? form.DateFrom?.Date : null,
            DateTo = form.IsCustom ? form.DateTo?.Date : null,
            AutoDateIntervalUnit = form.IsCustom ? null : NormalizeAutoDateIntervalUnit(form.AutoDateIntervalUnit),
            AutoDateIntervalValue = form.IsCustom ? null : form.AutoDateIntervalValue,
            IsCustom = form.IsCustom,
            EncryptedPassword = dataProtectionService.Protect(form.Password),
            IsActive = form.IsActive,
            CreatedByUserId = userId,
            CreatedByUsername = username,
            CreatedUtc = DateTime.UtcNow,
            HangfireJobId = generatedJobName
        };

        await scheduledJobRepository.AddAsync(entity);

        SyncRecurringJob(entity);
        return entity.Id;
    }

    public async Task UpdateAsync(ScheduledJobFormViewModel form, string userId, string username, string userDepartment)
    {
        ValidateForm(form);
        string sourceName;
        string? normalizedParameters;

        if (form.RequiresSchemaTemplateAndClientCode)
        {
            ReportDefinition report = await GetReportDefinitionAsync(form.ReportDefinitionId, userDepartment);
            sourceName = report.ReportName;
            normalizedParameters = await NormalizeScheduledParametersJsonAsync(form.Parameters, report);
        }
        else
        {
            sourceName = await GetDataFileNameAsync(form.ReportDefinitionId, userId, userDepartment);
            normalizedParameters = null;
        }

        ScheduledReportJob entity = await scheduledJobRepository.GetByIdForUserUpdateAsync(form.Id, userId)
            ?? throw new InvalidOperationException($"Scheduled job {form.Id} was not found.");

        string newJobName = await BuildJobNameAsync(form, userId, userDepartment, form.Id, sourceName);
        string oldHangfireJobId = entity.HangfireJobId;
        string newHangfireJobId = newJobName;

        entity.JobName = newJobName;
        entity.ReportDefinitionId = form.RequiresSchemaTemplateAndClientCode ? form.ReportDefinitionId : null;
        entity.DataFileDefinitionId = form.RequiresSchemaTemplateAndClientCode ? null : form.ReportDefinitionId;
        entity.Format = BuildFormatsStorageValue(form.Formats);
        entity.JobAction = NormalizeJobAction(form.JobAction);
        entity.RecipientEmail = NormalizeRecipientEmails(form.RecipientEmail);
        entity.CronExpression = BuildCronExpression(form);
        entity.SchemaTemplate = NormalizeNullable(form.SchemaTemplate);
        entity.ClientCode = NormalizeNullable(form.ClientCode);
        entity.Parameters = normalizedParameters;
        entity.IsExportToClientFolder = form.IsExportToClientFolder;
        entity.ExportLocation = NormalizeNullable(form.ExportLocation);
        entity.ExportToLocalFolder = form.ExportToLocalFolder;
        entity.DateFrom = form.IsCustom ? form.DateFrom?.Date : null;
        entity.DateTo = form.IsCustom ? form.DateTo?.Date : null;
        entity.AutoDateIntervalUnit = form.IsCustom ? null : NormalizeAutoDateIntervalUnit(form.AutoDateIntervalUnit);
        entity.AutoDateIntervalValue = form.IsCustom ? null : form.AutoDateIntervalValue;
        entity.IsCustom = form.IsCustom;
        entity.IsActive = form.IsActive;
        entity.UpdatedByUserId = userId;
        entity.UpdatedByUsername = username;
        entity.UpdatedUtc = DateTime.UtcNow;

        if (form.UpdatePassword)
        {
            if (string.IsNullOrWhiteSpace(form.Password))
            {
                throw new InvalidOperationException("Password is required when update password is enabled.");
            }

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

    public async Task DeleteAsync(int id, string userId)
    {
        ScheduledReportJob entity = await scheduledJobRepository.GetByIdForUserUpdateAsync(id, userId)
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

    private async Task<List<ReportDefinitionLookupItem>> GetReportLookupAsync(string? userDepartment)
    {
        List<ReportDefinition> reports = await reportRepository.GetAllReportsAsync(userDepartment);
        return reports
            .Select(report => new ReportDefinitionLookupItem { Id = report.Id, ReportName = report.ReportName })
            .ToList();
    }

    private async Task<List<ReportDefinitionLookupItem>> GetDataFileLookupAsync(string userId, string? userDepartment)
    {
        List<DataFileDefinition> reports = await dataFileManageRepository.GetAllWithColumnsAsync(userId, userDepartment);
        return reports
            .Select(report => new ReportDefinitionLookupItem { Id = report.Id, ReportName = report.DataFileName })
            .ToList();
    }

    private async Task<ReportDefinition> GetReportDefinitionAsync(int reportDefinitionId, string userDepartment)
    {
        List<ReportDefinition> reports = await reportRepository.GetAllReportsAsync(userDepartment);
        return reports.FirstOrDefault(candidate => candidate.Id == reportDefinitionId)
            ?? throw new InvalidOperationException("Report was not found.");
    }

    private async Task<string> GetDataFileNameAsync(int dataFileDefinitionId, string userId, string userDepartment)
    {
        DataFileManageListViewModel dataFileList = await dataFileManageService.GetListViewModelAsync(userId, userDepartment);
        DataFileDefinition? dataFile = dataFileList.DataFiles
            .FirstOrDefault(candidate => candidate.Id == dataFileDefinitionId && candidate.IsActive);

        return dataFile?.DataFileName
            ?? throw new InvalidOperationException("Data file was not found.");
    }

    private async Task<Dictionary<int, List<string>>> GetClientCodesLookupAsync(
        string userId,
        IEnumerable<ReportDefinitionLookupItem> reports)
    {
        Dictionary<int, List<string>> clientCodesByReportId = [];

        foreach (ReportDefinitionLookupItem report in reports)
        {
            clientCodesByReportId[report.Id] = await columnPreferenceRepository.GetSchemaTemplatesAsync(userId, report.Id);
        }

        return clientCodesByReportId;
    }

    private async Task<Dictionary<int, List<string>>> GetDataFileSchemaTemplatesLookupAsync(
        string userId,
        IEnumerable<ReportDefinitionLookupItem> reports)
    {
        Dictionary<int, List<string>> clientCodesByReportId = [];

        foreach (ReportDefinitionLookupItem report in reports)
        {
            clientCodesByReportId[report.Id] = await columnPreferenceRepository.GetDataFileSchemaTemplatesAsync(userId, report.Id);
        }

        return clientCodesByReportId;
    }

    private static List<string> BuildAvailableSchemaTemplates(string? currentSchemaTemplate, IEnumerable<string> savedSchemaTemplates)
        => savedSchemaTemplates
            .Append(currentSchemaTemplate ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static List<string> BuildAvailableClientCodes(string? currentClientCode, IEnumerable<string> clientCodes)
        => clientCodes
            .Append(currentClientCode ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private async Task<Dictionary<int, List<ScheduledJobParameterInputViewModel>>> BuildParameterLookupByReportIdAsync(
        IEnumerable<ReportDefinitionLookupItem> reports,
        string? userDepartment)
    {
        List<ReportDefinition> reportDefinitions = await reportRepository.GetAllReportsAsync(userDepartment);
        Dictionary<int, ReportDefinition> reportById = reportDefinitions
            .GroupBy(report => report.Id)
            .ToDictionary(group => group.Key, group => group.First());

        Dictionary<int, List<ScheduledJobParameterInputViewModel>> parametersByReportId = [];
        foreach (ReportDefinitionLookupItem report in reports)
        {
            if (!reportById.TryGetValue(report.Id, out ReportDefinition? reportDefinition))
            {
                parametersByReportId[report.Id] = [];
                continue;
            }

            parametersByReportId[report.Id] = await GetScheduledParametersAsync(reportDefinition);
        }

        return parametersByReportId;
    }

    private async Task<List<ScheduledJobParameterInputViewModel>> GetScheduledParametersAsync(ReportDefinition report)
    {
        if (string.IsNullOrWhiteSpace(report.SourceSP))
        {
            return [];
        }

        List<string> sourceParameterNames = await reportRepository.GetStoredProcedureParameterNamesAsync(report.SourceSP);
        Dictionary<string, string?> configuredDefaults = ParseReportParameterDefaults(report.Parameters);

        List<ScheduledJobParameterInputViewModel> items = [];
        foreach (string sourceParameterName in sourceParameterNames)
        {
            string queryKey = NormalizeParameterName(sourceParameterName);
            if (ReservedParameterNames.Contains(queryKey))
            {
                continue;
            }

            string? defaultValue = configuredDefaults.TryGetValue(queryKey, out string? configuredDefault)
                ? configuredDefault
                : null;

            items.Add(new ScheduledJobParameterInputViewModel
            {
                Name = "@" + queryKey,
                QueryKey = queryKey,
                DefaultValue = defaultValue,
                IsRequired = string.IsNullOrWhiteSpace(defaultValue)
            });
        }

        return items;
    }

    private async Task<string?> NormalizeScheduledParametersJsonAsync(string? inputJson, ReportDefinition report)
    {
        List<ScheduledJobParameterInputViewModel> scheduledParameters = await GetScheduledParametersAsync(report);
        if (scheduledParameters.Count == 0)
        {
            return null;
        }

        Dictionary<string, string?> inputValues = ParseScheduledParameterValues(inputJson);
        List<ScheduledJobParameterValue> result = [];

        foreach (ScheduledJobParameterInputViewModel parameter in scheduledParameters)
        {
            bool hasInput = inputValues.TryGetValue(parameter.QueryKey, out string? rawValue);
            string? inputValue = rawValue?.Trim();
            string? effectiveValue = !string.IsNullOrWhiteSpace(inputValue)
                ? inputValue
                : parameter.DefaultValue?.Trim();

            if (parameter.IsRequired && string.IsNullOrWhiteSpace(effectiveValue))
            {
                throw new InvalidOperationException($"Parameter {parameter.Name} is required for this report.");
            }

            if (!hasInput && string.IsNullOrWhiteSpace(effectiveValue))
            {
                continue;
            }

            result.Add(new ScheduledJobParameterValue
            {
                Name = parameter.Name,
                Value = effectiveValue
            });
        }

        return result.Count == 0 ? null : JsonSerializer.Serialize(result);
    }

    private static Dictionary<string, string?> ParseReportParameterDefaults(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            List<ReportParameterFormModel> parameters = JsonSerializer.Deserialize<List<ReportParameterFormModel>>(json)
                ?? [];

            return parameters
                .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Name))
                .GroupBy(parameter => NormalizeParameterName(parameter.Name), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.First().DefaultValue,
                    StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static Dictionary<string, string?> ParseScheduledParameterValues(string? json)
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

    private async Task<string> BuildJobNameAsync(ScheduledJobFormViewModel form, string userId, string userDepartment, int? currentJobId = null, string? sourceNameOverride = null)
    {
        string sourceName;
        if (!string.IsNullOrWhiteSpace(sourceNameOverride))
        {
            sourceName = sourceNameOverride;
        }
        else
        {
            List<ReportDefinition> reports = await reportRepository.GetAllReportsAsync(userDepartment);
            ReportDefinition report = reports.FirstOrDefault(candidate => candidate.Id == form.ReportDefinitionId)
                ?? throw new InvalidOperationException("Report was not found.");
            sourceName = report.ReportName;
        }

        string baseJobName = string.Join('-',
        [
            NormalizeJobNameSegment(userId, "user"),
            NormalizeJobNameSegment(sourceName, "source"),
            NormalizeJobNameSegment(form.SchemaTemplate, "default"),
            NormalizeJobNameSegment(form.ClientCode, "all")
        ]);

        List<ScheduledReportJob> existingJobs = await scheduledJobRepository.GetAllByUserIdAsync(userId);
        HashSet<string> existingNames = existingJobs
            .Where(job => currentJobId is null || job.Id != currentJobId.Value)
            .Select(job => job.JobName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return BuildUniqueJobName(baseJobName, existingNames);
    }

    private static string NormalizeJobNameSegment(string? value, string fallback)
    {
        string normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return fallback;
        }

        StringBuilder builder = new(normalized.Length);
        bool previousWasSeparator = false;

        foreach (char character in normalized)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSeparator = false;
                continue;
            }

            if (previousWasSeparator)
            {
                continue;
            }

            builder.Append('-');
            previousWasSeparator = true;
        }

        string sanitized = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(sanitized) ? fallback : sanitized;
    }

    private static string BuildUniqueJobName(string baseJobName, ISet<string> existingNames)
    {
        string candidate = TruncateJobName(baseJobName);
        if (!existingNames.Contains(candidate))
        {
            return candidate;
        }

        for (int suffix = 2; ; suffix++)
        {
            string suffixText = $"-{suffix}";
            candidate = TruncateJobName(baseJobName, suffixText);

            if (!existingNames.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    private static string TruncateJobName(string baseJobName, string suffix = "")
    {
        int maxBaseLength = ScheduledJobFormViewModel.JobNameMaxLength - suffix.Length;
        string truncatedBaseName = baseJobName.Length <= maxBaseLength
            ? baseJobName
            : baseJobName[..maxBaseLength].TrimEnd('-');

        if (string.IsNullOrWhiteSpace(truncatedBaseName))
        {
            truncatedBaseName = "job";
        }

        string candidate = $"{truncatedBaseName}{suffix}";
        return candidate.Length <= ScheduledJobFormViewModel.JobNameMaxLength
            ? candidate
            : candidate[..ScheduledJobFormViewModel.JobNameMaxLength];
    }


    private static string? NormalizeNullable(string? value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string? ExtractExportLocationSubfolder(string? exportLocation)
    {
        string normalized = (exportLocation ?? string.Empty).Trim().TrimEnd('\\', '/');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        string? subfolder = Path.GetFileName(normalized);
        return string.IsNullOrWhiteSpace(subfolder) ? null : subfolder;
    }

    private static string NormalizeJobAction(string? value)
    {
        string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalized) ? ScheduledJobActions.ExportFile : normalized;
    }

    private static void ValidateForm(ScheduledJobFormViewModel form)
    {
        if (form.ReportDefinitionId <= 0)
        {
            throw new InvalidOperationException("Report is required.");
        }

        List<string> normalizedFormats = NormalizeFormats(form.Formats);
        if (normalizedFormats.Count == 0)
        {
            throw new InvalidOperationException("At least one format must be selected.");
        }

        string normalizedJobAction = NormalizeJobAction(form.JobAction);
        if (!ScheduledJobActions.All.Contains(normalizedJobAction, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Job action is invalid.");
        }

        if (normalizedJobAction == ScheduledJobActions.ExportFileAndEmailToUser)
        {
            List<string> recipientEmails = ParseRecipientEmails(form.RecipientEmail);
            if (recipientEmails.Count == 0)
            {
                throw new InvalidOperationException("Recipient email is required and must contain at least one valid email address when using export file and email to user action.");
            }
        }

        string normalizedScheduleType = (form.ScheduleType ?? string.Empty).Trim().ToLowerInvariant();
        if (normalizedScheduleType is not (
            ScheduledJobFormViewModel.ScheduleTypeEveryMinutes or
            ScheduledJobFormViewModel.ScheduleTypeDailyTime or
            ScheduledJobFormViewModel.ScheduleTypeCustomDays or
            ScheduledJobFormViewModel.ScheduleTypeAdvancedCron))
        {
            throw new InvalidOperationException("Schedule type is invalid.");
        }

        _ = BuildCronExpression(form);

        if (form.Id == 0 && string.IsNullOrWhiteSpace(form.Password))
        {
            throw new InvalidOperationException("Password is required for a new scheduled job.");
        }

        if (form.Id > 0 && form.UpdatePassword && string.IsNullOrWhiteSpace(form.Password))
        {
            throw new InvalidOperationException("Password is required when update password is enabled.");
        }

        if (form.RequiresSchemaTemplateAndClientCode)
        {
            //if (string.IsNullOrWhiteSpace(form.SchemaTemplate))
            //{
            //    throw new InvalidOperationException("Schema Template is required.");
            //}

            if (string.IsNullOrWhiteSpace(form.ClientCode))
            {
                throw new InvalidOperationException("Client Code is required.");
            }
        }

        if (form.IsCustom &&
            form.DateFrom.HasValue &&
            form.DateTo.HasValue &&
            form.DateFrom.Value.Date > form.DateTo.Value.Date)
        {
            throw new InvalidOperationException("Date From cannot be later than Date To.");
        }

        if (!form.IsCustom)
        {
            string? normalizedAutoDateIntervalUnit = NormalizeAutoDateIntervalUnit(form.AutoDateIntervalUnit);
            if (normalizedAutoDateIntervalUnit is null)
            {
                throw new InvalidOperationException("Auto Date Interval must be one of Minutely, Hourly, Daily, Weekly, Monthly, or Yearly.");
            }

            if (!form.AutoDateIntervalValue.HasValue || form.AutoDateIntervalValue.Value < 1)
            {
                throw new InvalidOperationException("Interval Value must be greater than 0.");
            }
        }
    }

    private static string? NormalizeAutoDateIntervalUnit(string? value)
    {
        string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return SupportedAutoDateIntervalUnits.Contains(normalized, StringComparer.Ordinal)
            ? normalized
            : null;
    }

    private static List<string> ParseRecipientEmails(string? value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return [];
        }

        char[] separators = [',', ';', '\r', '\n'];
        string[] segments = normalized.Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        List<string> recipientEmails = segments
            .Select(email => email.Trim())
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (string recipientEmail in recipientEmails)
        {
            try
            {
                _ = new MailAddress(recipientEmail);
            }
            catch (FormatException ex)
            {
                throw new InvalidOperationException($"Recipient email '{recipientEmail}' is not valid.", ex);
            }
        }

        return recipientEmails;
    }

    private static string? NormalizeRecipientEmails(string? value)
    {
        List<string> recipientEmails = ParseRecipientEmails(value);
        return recipientEmails.Count == 0 ? null : string.Join("; ", recipientEmails);
    }

    private static List<string> ParseFormats(string? value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return ["csv"];
        }

        string[] segments = normalized.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return NormalizeFormats(segments);
    }

    private static List<string> NormalizeFormats(IEnumerable<string>? values)
    {
        List<string> normalizedValues = (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        List<string> invalidFormats = normalizedValues
            .Where(value => !SupportedExportFormats.Contains(value, StringComparer.Ordinal))
            .ToList();

        if (invalidFormats.Count > 0)
        {
            throw new InvalidOperationException("Format must be csv, excel, or pdf.");
        }

        return SupportedExportFormats
            .Where(value => normalizedValues.Contains(value, StringComparer.Ordinal))
            .ToList();
    }

    private static string BuildFormatsStorageValue(IEnumerable<string>? values)
        => string.Join(',', NormalizeFormats(values));

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

        if (scheduleType == ScheduledJobFormViewModel.ScheduleTypeCustomDays)
        {
            string rawTime = form.CustomDaysTime?.Trim() ?? string.Empty;
            if (!TimeOnly.TryParse(rawTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly time))
            {
                throw new InvalidOperationException("Execution time is invalid. Use HH:mm.");
            }

            List<int> selectedWeekdays = NormalizeWeekdays(form.SelectedWeekdays);
            if (selectedWeekdays.Count == 0)
            {
                throw new InvalidOperationException("At least one execution day is required for custom dates schedule.");
            }

            string daysSegment = string.Join(',', selectedWeekdays);
            return $"{time.Minute} {time.Hour} * * {daysSegment}";
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

        if (segments.Length >= 5 &&
            int.TryParse(segments[0], out minute) &&
            int.TryParse(segments[1], out hour) &&
            segments[2] == "*" &&
            segments[3] == "*" &&
            minute is >= 0 and < 60 &&
            hour is >= 0 and < 24 &&
            TryParseWeekdaysCronSegment(segments[4], out List<int> parsedWeekdays))
        {
            form.ScheduleType = ScheduledJobFormViewModel.ScheduleTypeCustomDays;
            form.CustomDaysTime = $"{hour:00}:{minute:00}";
            form.SelectedWeekdays = parsedWeekdays;
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

        if (segments.Length >= 5 &&
            int.TryParse(segments[0], out minute) &&
            int.TryParse(segments[1], out hour) &&
            segments[2] == "*" &&
            segments[3] == "*" &&
            minute is >= 0 and < 60 &&
            hour is >= 0 and < 24 &&
            TryParseWeekdaysCronSegment(segments[4], out List<int> weekdays))
        {
            return $"Custom days ({FormatWeekdayDisplay(weekdays)}) at {hour:00}:{minute:00}";
        }

        return "Advanced schedule";
    }

    private static List<int> NormalizeWeekdays(IEnumerable<int>? weekdays)
    {
        if (weekdays is null)
        {
            return [];
        }

        HashSet<int> uniqueWeekdays = [];

        foreach (int weekday in weekdays)
        {
            int normalizedWeekday = weekday == 7 ? 0 : weekday;
            if (normalizedWeekday is < 0 or > 6)
            {
                continue;
            }

            uniqueWeekdays.Add(normalizedWeekday);
        }

        return OrderedCronWeekdays
            .Where(uniqueWeekdays.Contains)
            .ToList();
    }

    private static bool TryParseWeekdaysCronSegment(string? segment, out List<int> weekdays)
    {
        weekdays = [];
        string value = segment?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value) || value == "*")
        {
            return false;
        }

        string[] parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        List<int> parsedWeekdays = [];
        foreach (string part in parts)
        {
            if (!int.TryParse(part, out int parsedDay))
            {
                return false;
            }

            parsedWeekdays.Add(parsedDay);
        }

        weekdays = NormalizeWeekdays(parsedWeekdays);
        return weekdays.Count > 0;
    }

    private static string FormatWeekdayDisplay(IEnumerable<int> weekdays)
    {
        List<string> weekdayNames = weekdays
            .Where(weekday => WeekdayDisplayNames.ContainsKey(weekday))
            .Select(weekday => WeekdayDisplayNames[weekday])
            .ToList();

        return weekdayNames.Count == 0
            ? "No days"
            : string.Join(", ", weekdayNames);
    }
}
