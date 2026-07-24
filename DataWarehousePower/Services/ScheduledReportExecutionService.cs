using DataWarehousePower.Helper;
using DataWarehousePower.Models;
using DataWarehousePower.Models.AppSettings;
using DataWarehousePower.Repositories;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace DataWarehousePower.Services;

public sealed class ScheduledReportExecutionService(
    IScheduledReportJobRepository scheduledJobRepository,
    IReportService reportService,
    IDataFileManageService dataFileManageService,
    IReportRepository reportRepository,
    IReportExportService reportExportService,
    IScheduledReportEmailService scheduledReportEmailService,
    IHangfireDataProtectionService dataProtectionService,
    IConfiguration configuration,
    IOptionsSnapshot<RemoteFolderExportLocationAppSetting> remoteFolderExportLocationAppSetting,
    ILogger<ScheduledReportExecutionService> logger) : IScheduledReportExecutionService
{
    private const string LocalExportBasePathSection = "ScheduledJob:LocalExportBasePath";
    private const string UseSnowflakeForDataFileSection = "ScheduledJob:UseSnowflakeForDataFile";

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
        var remoteFolderExportLocation = remoteFolderExportLocationAppSetting.Value;
        var userRemoteFolderExportLocation = Path.Combine(remoteFolderExportLocationAppSetting.Value.UserReportFolderPhysicalPath, job.CreatedByUserId, $"{DateTimeHelper.GetCurrentLocalTime():yyyy-MM-dd}");
        
        

        ReportViewModel? reportViewModel;
        if (job.ReportDefinitionId.HasValue)
        {
            reportViewModel = await reportService.BuildReportViewModelAsync(
                reportId: job.ReportDefinitionId.Value,
                userId: job.CreatedByUserId,
                userDepartment: job.CreatedByUserDepartment,
                schemaTemplate: job.SchemaTemplate,
                clientCode: job.ClientCode,
                dateFrom: effectiveDateFrom,
                dateTo: effectiveDateTo,
                parameterValues: jobParameters);
        }
        else if (job.DataFileDefinitionId.HasValue)
        {
            List<ColumnDefinition> dataFileFilterColumns = new List<ColumnDefinition>();

            if (!string.IsNullOrEmpty(job.FilterColumnDataJson))
            {
                dataFileFilterColumns = JsonSerializer.Deserialize<List<ColumnDefinition>>(job.FilterColumnDataJson);
            }

            reportViewModel = await BuildDataFileReportViewModelAsync(
                dataFileDefinitionId: job.DataFileDefinitionId.Value,
                userId: job.CreatedByUserId,
                userDepartment: job.CreatedByUserDepartment,
                schemaTemplate: job.SchemaTemplate,
                dataFileFilterColumns: dataFileFilterColumns,
                recurringDataDateColumn: job.RecurringDataDateColumn,
                clientCode: job.ClientCode,
                dateFrom: effectiveDateFrom,
                dateTo: effectiveDateTo,
                parameterValues: jobParameters);
        }
        else
        {
            logger.LogWarning(
                "Scheduled export job {ScheduledJobId} skipped because no source definition id was provided.",
                scheduledJobId);
            return;
        }

        if (reportViewModel is null)
        {
            logger.LogWarning(
                "Scheduled export job {ScheduledJobId} skipped because source was not found. ReportDefinitionId={ReportDefinitionId}, DataFileDefinitionId={DataFileDefinitionId}",
                scheduledJobId,
                job.ReportDefinitionId,
                job.DataFileDefinitionId);
            return;
        }

        string reportDate = BuildReportDateSegment(effectiveDateFrom, effectiveDateTo);

        string safeReportName = string.Join("_", reportViewModel.ReportName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        string clientCodeSegment = string.IsNullOrWhiteSpace(job.ClientCode) ? "all" : job.ClientCode.Trim();
        string fileName = $"{safeReportName}_{clientCodeSegment}_{reportDate}_({DateTimeHelper.GetCurrentLocalTime():yyyy-MM-dd_HHmm}).zip";
        string zipSubFileName = $"{safeReportName}_format_{clientCodeSegment}_{reportDate}_({DateTimeHelper.GetCurrentLocalTime():yyyy-MM-dd_HHmm})";
        CsvExportSplitOptions? exportSplitOptions = ResolveExportSplitOptions(configuration, logger, scheduledJobId);

        byte[] zipBytes = await reportExportService.BuildPasswordProtectedZipAsync(
            reportViewModel,
            normalizedFormats,
            password,
            zipSubFileName,
            exportSplitOptions);

        string jobAction = (job.JobAction ?? string.Empty).Trim().ToLowerInvariant();
        if (jobAction == ScheduledJobActions.ExportFile || jobAction == ScheduledJobActions.ExportFileAndEmailToUser)
        {
            List<string> targetDirectories = new List<string>();

            if (job.IsExportToClientFolder)
            {
                string primaryDirectory = ResolveExportDirectory(job.ExportLocation);
                targetDirectories = [primaryDirectory];
            }

            if (job.ExportToLocalFolder)
            {
                string? localDirectory = ResolveLocalExportDirectory(job, userRemoteFolderExportLocation);
                if (!string.IsNullOrWhiteSpace(localDirectory) &&
                !targetDirectories.Contains(localDirectory, StringComparer.OrdinalIgnoreCase))
                {
                    targetDirectories.Add(localDirectory);
                }
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
        }
        
        if (jobAction == ScheduledJobActions.ExportFileAndEmailToUser || jobAction == ScheduledJobActions.EmailToUser)
        {
            if (string.IsNullOrWhiteSpace(job.RecipientEmail))
            {
                throw new InvalidOperationException($"Scheduled job {scheduledJobId} is configured for email action but recipient email is empty.");
            }

            string emailSubject = $"{safeReportName} {clientCodeSegment} {reportDate}";
            string emailBody = $"The {safeReportName} for {reportDate} was automatically exported for {clientCodeSegment}, and the ZIP file is attached.";

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

    private async Task<ReportViewModel?> BuildDataFileReportViewModelAsync(
        int dataFileDefinitionId,
        string userId,
        string? userDepartment = null,
        string? schemaTemplate = null,
        List<ColumnDefinition>? dataFileFilterColumns = null,
        string? recurringDataDateColumn = null,
        string? clientCode = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        IReadOnlyDictionary<string, string?>? parameterValues = null)
    {
        string normalizedSchemaTemplate = NormalizeSchemaTemplate(schemaTemplate);

        DataFileManageListViewModel dataFileList = await dataFileManageService.GetListViewModelAsync();
        DataFileDefinition? dataFile = dataFileList.DataFiles
            .FirstOrDefault(candidate => candidate.Id == dataFileDefinitionId && candidate.IsActive);

        if (dataFile is null)
        {
            return null;
        }

        var dataFileExecFilterColumns = dataFileFilterColumns?.Select(column => new DataFileColumn
        {
            PropertyName = column.PropertyName,
            PropertyType = column.PropertyType,
            DefaultLabel = column.DefaultLabel,
            MappingParameter = column.MappingParameterFilter,
            MappingParameterFilter = column.MappingParameter,
            DisplayOrder = column.Order
        }).ToList();

        List<ColumnDefinition> systemColumns = dataFile.Columns
            .OrderBy(column => column.DisplayOrder)
            .Select(column => new ColumnDefinition
            {
                Key = column.PropertyName,
                DefaultLabel = column.DefaultLabel,
                DisplayLabel = column.DefaultLabel,
                IsVisible = true,
                Order = column.DisplayOrder
            })
            .ToList();

        // Apply user preferences
        (List<ColumnDefinition> displayColumns, int? activePreferenceId) =
            await reportService.LoadDataFileColumnPreferencesAsync(userId, dataFileDefinitionId, normalizedSchemaTemplate, systemColumns);

        List<Dictionary<string, object?>> rows;
        if (!string.IsNullOrWhiteSpace(dataFile.SourceSP))
        {
            rows = await reportRepository.GetReportDataFromSpAsync(
                dataFile.SourceSP,
                clientCode,
                dateFrom,
                dateTo,
                parameterValues);
        }
        else if (!string.IsNullOrWhiteSpace(dataFile.SourceTable))
        {
            List<string> columnNames = dataFile.Columns
                .Select(column => column.PropertyName)
                .Where(columnName => !string.IsNullOrWhiteSpace(columnName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            bool useSnowflakeForDataFile = configuration.GetValue<bool>(UseSnowflakeForDataFileSection);
            if (useSnowflakeForDataFile)
            {
                rows = await reportRepository.GetDataFileDataFromTableSnowflakeAsync(
                    userId,
                    userDepartment,
                    dataFile.SourceTable,
                    dataFileExecFilterColumns,
                    dataFile.SourceDatabase,
                    dataFile.FilterClientCodeColumn,
                    recurringDataDateColumn,
                    clientCode,
                    dateFrom,
                    dateTo);
            }
            else
            {
                rows = await reportRepository.GetDataFileDataFromTableAsync(
                    userId,
                    dataFile.SourceTable,
                    dataFileExecFilterColumns,
                    dataFile.SourceDatabase,
                    dataFile.FilterClientCodeColumn,
                    recurringDataDateColumn,
                    clientCode,
                    dateFrom,
                    dateTo);
            }

            //rows = await reportRepository.GetDataFileDataFromTableAsync(
            //    userId,
            //    dataFile.SourceTable,
            //    dataFileExecFilterColumns,
            //    dataFile.SourceDatabase,
            //    dataFile.FilterClientCodeColumn,
            //    recurringDataDateColumn,
            //    clientCode,
            //    dateFrom,
            //    dateTo);

            //rows = await reportRepository.GetDataFileDataFromTableAsync(
            //    dataFile.SourceTable,
            //    dataFileExecFilterColumns,
            //    dataFile.SourceDatabase,
            //    dataFile.FilterClientCodeColumn,
            //    recurringDataDateColumn,
            //    clientCode,
            //    dateFrom,
            //    dateTo);

            //rows = ApplyMappedDataFileFilters(rows, dataFile.Columns, clientCode, dateFrom, dateTo, parameterValues);
        }
        else
        {
            rows = [];
        }

        return new ReportViewModel
        {
            ReportId = dataFile.Id,
            ReportName = dataFile.DataFileName,
            ClientCode = clientCode?.Trim() ?? string.Empty,
            HasAppliedFilters = true,
            FilterDateFrom = dateFrom,
            FilterDateTo = dateTo,
            AvailableColumns = displayColumns,
            DisplayColumns = displayColumns,
            Rows = rows
        };
    }

    private async Task<ReportViewModel?> BuildDataFileReportViewModelAsync(
        int dataFileDefinitionId,
        string userId,
        string? userDepartment = null,
        string? schemaTemplate = null,
        string? clientCode = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        IReadOnlyDictionary<string, string?>? parameterValues = null)
    {
        string normalizedSchemaTemplate = NormalizeSchemaTemplate(schemaTemplate);

        DataFileManageListViewModel dataFileList = await dataFileManageService.GetListViewModelAsync();
        DataFileDefinition? dataFile = dataFileList.DataFiles
            .FirstOrDefault(candidate => candidate.Id == dataFileDefinitionId && candidate.IsActive);

        if (dataFile is null)
        {
            return null;
        }

        List<ColumnDefinition> systemColumns = dataFile.Columns
            .OrderBy(column => column.DisplayOrder)
            .Select(column => new ColumnDefinition
            {
                Key = column.PropertyName,
                DefaultLabel = column.DefaultLabel,
                DisplayLabel = column.DefaultLabel,
                IsVisible = true,
                Order = column.DisplayOrder
            })
            .ToList();

        // Apply user preferences
        (List<ColumnDefinition> displayColumns, int? activePreferenceId) =
            await reportService.LoadDataFileColumnPreferencesAsync(userId, dataFileDefinitionId, normalizedSchemaTemplate, systemColumns);

        List<Dictionary<string, object?>> rows;
        if (!string.IsNullOrWhiteSpace(dataFile.SourceSP))
        {
            rows = await reportRepository.GetReportDataFromSpAsync(
                dataFile.SourceSP,
                clientCode,
                dateFrom,
                dateTo,
                parameterValues);
        }
        else if (!string.IsNullOrWhiteSpace(dataFile.SourceTable))
        {
            List<string> columnNames = dataFile.Columns
                .Select(column => column.PropertyName)
                .Where(columnName => !string.IsNullOrWhiteSpace(columnName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            bool useSnowflakeForDataFile = configuration.GetValue<bool>(UseSnowflakeForDataFileSection);
            if (useSnowflakeForDataFile)
            {
                rows = await reportRepository.GetDataFileDataFromTableSnowflakeAsync(
                    userId,
                    userDepartment,
                    dataFile.SourceTable,
                    dataFile.Columns,
                    dataFile.SourceDatabase,
                    dataFile.FilterClientCodeColumn,
                    dataFile.FilterDateColumn,
                    clientCode,
                    dateFrom,
                    dateTo);
            }
            else
            {
                rows = await reportRepository.GetDataFileDataFromTableAsync(
                    dataFile.SourceTable,
                    dataFile.Columns,
                    dataFile.SourceDatabase,
                    dataFile.FilterClientCodeColumn,
                    dataFile.FilterDateColumn,
                    clientCode,
                    dateFrom,
                    dateTo);
            }

            //rows = ApplyMappedDataFileFilters(rows, dataFile.Columns, clientCode, dateFrom, dateTo, parameterValues);
        }
        else
        {
            rows = [];
        }

        return new ReportViewModel
        {
            ReportId = dataFile.Id,
            ReportName = dataFile.DataFileName,
            ClientCode = clientCode?.Trim() ?? string.Empty,
            HasAppliedFilters = true,
            FilterDateFrom = dateFrom,
            FilterDateTo = dateTo,
            AvailableColumns = displayColumns,
            DisplayColumns = displayColumns,
            Rows = rows
        };
    }

    private static List<Dictionary<string, object?>> ApplyMappedDataFileFilters(
        List<Dictionary<string, object?>> rows,
        IEnumerable<DataFileColumn> dataFileColumns,
        string? clientCode,
        DateTime? dateFrom,
        DateTime? dateTo,
        IReadOnlyDictionary<string, string?> parameterValues)
    {
        Dictionary<string, List<string>> columnsByParameter = dataFileColumns
            .Where(column => !string.IsNullOrWhiteSpace(column.PropertyName))
            .SelectMany(column => GetMappingParameters(column.MappingParameter)
                .Select(parameterName => new
                {
                    ParameterName = parameterName,
                    ColumnName = column.PropertyName
                }))
            .GroupBy(item => item.ParameterName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.ColumnName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);

        IEnumerable<Dictionary<string, object?>> filteredRows = rows;

        if (!string.IsNullOrWhiteSpace(clientCode) &&
            columnsByParameter.TryGetValue("ClientCode", out List<string>? clientCodeColumns))
        {
            filteredRows = filteredRows.Where(row => RowMatchesMappedValue(row, clientCodeColumns, clientCode));
        }

        if (dateFrom.HasValue &&
            TryGetMappedColumns(columnsByParameter, ["DateFrom", "FilterDateFrom"], out List<string> dateFromColumns))
        {
            filteredRows = filteredRows.Where(row => RowMatchesMappedDate(row, dateFromColumns, dateFrom.Value.Date, isLowerBound: true));
        }

        if (dateTo.HasValue &&
            TryGetMappedColumns(columnsByParameter, ["DateTo", "FilterDateTo"], out List<string> dateToColumns))
        {
            filteredRows = filteredRows.Where(row => RowMatchesMappedDate(row, dateToColumns, dateTo.Value.Date, isLowerBound: false));
        }

        foreach ((string parameterName, string? parameterValue) in parameterValues)
        {
            if (string.IsNullOrWhiteSpace(parameterValue) ||
                !columnsByParameter.TryGetValue(parameterName, out List<string>? mappedColumns))
            {
                continue;
            }

            filteredRows = filteredRows.Where(row => RowMatchesMappedValue(row, mappedColumns, parameterValue));
        }

        // Support literal MappingParameter values on individual columns.
        // Example: MappingParameter="Sam" on ColumnA means ColumnA LIKE "%Sam%".
        filteredRows = ApplyLiteralMappingParameterFilters(filteredRows, dataFileColumns, parameterValues);

        return filteredRows.ToList();
    }

    private static IEnumerable<Dictionary<string, object?>> ApplyLiteralMappingParameterFilters(
        IEnumerable<Dictionary<string, object?>> rows,
        IEnumerable<DataFileColumn> dataFileColumns,
        IReadOnlyDictionary<string, string?> parameterValues)
    {
        HashSet<string> parameterNames = parameterValues.Keys
            .Select(NormalizeFilterParameterAlias)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        IEnumerable<Dictionary<string, object?>> filteredRows = rows;

        foreach (DataFileColumn column in dataFileColumns)
        {
            string columnName = column.PropertyName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(columnName))
            {
                continue;
            }

            List<string> literalTerms = GetMappingParameters(column.MappingParameter)
                .Where(term =>
                    !term.Equals("ClientCode", StringComparison.OrdinalIgnoreCase) &&
                    !term.Equals("DateFrom", StringComparison.OrdinalIgnoreCase) &&
                    !term.Equals("DateTo", StringComparison.OrdinalIgnoreCase) &&
                    !parameterNames.Contains(term))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (literalTerms.Count == 0)
            {
                continue;
            }

            filteredRows = filteredRows.Where(row => RowMatchesAnyContainsTerm(row, columnName, literalTerms));
        }

        return filteredRows;
    }

    private static bool TryGetMappedColumns(
        IReadOnlyDictionary<string, List<string>> columnsByParameter,
        IEnumerable<string> aliases,
        out List<string> mappedColumns)
    {
        List<string> combinedColumns = [];

        foreach (string alias in aliases)
        {
            if (columnsByParameter.TryGetValue(alias, out List<string>? aliasColumns))
            {
                combinedColumns.AddRange(aliasColumns);
            }
        }

        mappedColumns = combinedColumns
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return mappedColumns.Count > 0;
    }

    private static IEnumerable<string> GetMappingParameters(string? mappingParameter)
        => (mappingParameter ?? string.Empty)
            .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeFilterParameterAlias)
            .Where(parameterName => !string.IsNullOrWhiteSpace(parameterName));

    private static string NormalizeFilterParameterAlias(string parameterName)
    {
        string normalized = NormalizeMappingParameterName(parameterName);
        if (normalized.Equals("FilterDateFrom", StringComparison.OrdinalIgnoreCase))
        {
            return "DateFrom";
        }

        if (normalized.Equals("FilterDateTo", StringComparison.OrdinalIgnoreCase))
        {
            return "DateTo";
        }

        if (normalized.Equals("FilterClientCode", StringComparison.OrdinalIgnoreCase))
        {
            return "ClientCode";
        }

        return normalized;
    }

    private static string NormalizeMappingParameterName(string? name)
        => (name ?? string.Empty).Trim().TrimStart('@');

    private static bool RowMatchesMappedValue(
        IReadOnlyDictionary<string, object?> row,
        IEnumerable<string> columnNames,
        string expectedValue)
    {
        string normalizedExpectedValue = expectedValue.Trim();
        if (string.IsNullOrWhiteSpace(normalizedExpectedValue))
        {
            return true;
        }

        foreach (string columnName in columnNames)
        {
            if (!row.TryGetValue(columnName, out object? rawValue) || rawValue is null)
            {
                continue;
            }

            string actualValue = Convert.ToString(rawValue)?.Trim() ?? string.Empty;
            if (actualValue.Equals(normalizedExpectedValue, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool RowMatchesMappedDate(
        IReadOnlyDictionary<string, object?> row,
        IEnumerable<string> columnNames,
        DateTime boundary,
        bool isLowerBound)
    {
        foreach (string columnName in columnNames)
        {
            if (!row.TryGetValue(columnName, out object? rawValue) || !TryConvertToDate(rawValue, out DateTime valueDate))
            {
                continue;
            }

            if (isLowerBound ? valueDate.Date >= boundary : valueDate.Date <= boundary)
            {
                return true;
            }
        }

        return false;
    }

    private static bool RowMatchesAnyContainsTerm(
        IReadOnlyDictionary<string, object?> row,
        string columnName,
        IEnumerable<string> terms)
    {
        if (!row.TryGetValue(columnName, out object? rawValue) || rawValue is null)
        {
            return false;
        }

        string actualValue = Convert.ToString(rawValue) ?? string.Empty;
        foreach (string term in terms)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                continue;
            }

            if (actualValue.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryConvertToDate(object? rawValue, out DateTime valueDate)
    {
        switch (rawValue)
        {
            case DateTime dateTime:
                valueDate = dateTime;
                return true;
            case DateTimeOffset dateTimeOffset:
                valueDate = dateTimeOffset.DateTime;
                return true;
            case DateOnly dateOnly:
                valueDate = dateOnly.ToDateTime(TimeOnly.MinValue);
                return true;
            default:
                if (DateTime.TryParse(Convert.ToString(rawValue), out DateTime parsed))
                {
                    valueDate = parsed;
                    return true;
                }

                valueDate = default;
                return false;
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

    private static string? ResolveLocalExportDirectory(ScheduledReportJob job, string remoteFolderExportLocation)
    {
        if (!job.ExportToLocalFolder)
        {
            return null;
        }

        return remoteFolderExportLocation;

        //string subfolder = ExtractExportSubfolder(job.ExportLocation);
        //if (string.IsNullOrWhiteSpace(subfolder))
        //{
        //    return null;
        //}

        //string configuredBasePath = remoteFolderExportLocation;
        //string expandedBasePath = Environment.ExpandEnvironmentVariables(configuredBasePath).Trim();
        //if (string.IsNullOrWhiteSpace(expandedBasePath))
        //{
        //    expandedBasePath = Path.GetTempPath();
        //}

        //if (!Path.IsPathRooted(expandedBasePath))
        //{
        //    expandedBasePath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), expandedBasePath));
        //}

        //return Path.Combine(expandedBasePath, subfolder);
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

        if (job.AutoDateIntervalValue is > 0)
        {
            DateTime now = DateTimeHelper.GetCurrentLocalTime();
            string normalizedIntervalUnit = (job.AutoDateIntervalUnit ?? string.Empty).Trim().ToLowerInvariant();
            DateTime? intervalDateFrom = normalizedIntervalUnit switch
            {
                ScheduledJobFormViewModel.AutoDateIntervalMinutely => now.AddMinutes(-job.AutoDateIntervalValue.Value),
                ScheduledJobFormViewModel.AutoDateIntervalHourly => now.AddHours(-job.AutoDateIntervalValue.Value),
                ScheduledJobFormViewModel.AutoDateIntervalDaily => now.AddDays(-job.AutoDateIntervalValue.Value),
                ScheduledJobFormViewModel.AutoDateIntervalWeekly => now.AddDays(-(7 * job.AutoDateIntervalValue.Value)),
                ScheduledJobFormViewModel.AutoDateIntervalMonthly => now.AddMonths(-job.AutoDateIntervalValue.Value),
                ScheduledJobFormViewModel.AutoDateIntervalYearly => now.AddYears(-job.AutoDateIntervalValue.Value),
                ScheduledJobFormViewModel.AutoDateIntervalLastDay => now.Date.AddDays(-job.AutoDateIntervalValue.Value),
                ScheduledJobFormViewModel.AutoDateIntervalLastMonth => new DateTime(now.Year, now.Month, 1).AddMonths(-job.AutoDateIntervalValue.Value),
                ScheduledJobFormViewModel.AutoDateIntervalLastYear => new DateTime(now.Year, 1, 1).AddYears(-job.AutoDateIntervalValue.Value),
                _ => null
            };

            DateTime? intervalDateTo = normalizedIntervalUnit switch
            {
                ScheduledJobFormViewModel.AutoDateIntervalLastDay => now.Date.AddSeconds(-1),
                ScheduledJobFormViewModel.AutoDateIntervalLastMonth => new DateTime(now.Year, now.Month, 1).AddSeconds(-1),
                ScheduledJobFormViewModel.AutoDateIntervalLastYear => new DateTime(now.Year, 1, 1).AddSeconds(-1),
                _ => now
            };

            if (intervalDateFrom.HasValue)
            {
                return (intervalDateFrom.Value, intervalDateTo.Value);
            }
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

    private static string BuildReportDateSegment(DateTime? dateFrom, DateTime? dateTo)
    {
        if (!dateFrom.HasValue && !dateTo.HasValue)
        {
            return "no-date-filter";
        }

        if (dateFrom.HasValue && dateTo.HasValue)
        {
            bool includesTime =
                dateFrom.Value.TimeOfDay != TimeSpan.Zero ||
                dateTo.Value.TimeOfDay != TimeSpan.Zero;

            string format = includesTime ? "yyyy-MM-dd_HHmm" : "yyyy-MM-dd";
            return dateFrom.Value == dateTo.Value
                ? dateFrom.Value.ToString(format)
                : $"{dateFrom.Value.ToString(format)}_{dateTo.Value.ToString(format)}";
        }

        DateTime singleDate = dateFrom ?? dateTo!.Value;
        string singleFormat = singleDate.TimeOfDay == TimeSpan.Zero ? "yyyy-MM-dd" : "yyyy-MM-dd_HHmm";
        return singleDate.ToString(singleFormat);
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

    private static CsvExportSplitOptions? ResolveExportSplitOptions(
        IConfiguration configuration,
        ILogger logger,
        int scheduledJobId)
    {
        int? maxRowsPerFile = configuration.GetValue<int?>("ScheduledJob:ExportSplit:MaxRowsPerFile")
            ?? configuration.GetValue<int?>("ScheduledJob:CsvSplit:MaxRowsPerFile");
        long? maxBytesPerFile = configuration.GetValue<long?>("ScheduledJob:ExportSplit:MaxBytesPerFile")
            ?? configuration.GetValue<long?>("ScheduledJob:CsvSplit:MaxBytesPerFile");
        int? maxFileSizeMb = configuration.GetValue<int?>("ScheduledJob:ExportSplit:MaxFileSizeMb")
            ?? configuration.GetValue<int?>("ScheduledJob:CsvSplit:MaxFileSizeMb");

        if (!maxBytesPerFile.HasValue && maxFileSizeMb is > 0)
        {
            maxBytesPerFile = maxFileSizeMb.Value * 1024L * 1024L;
        }

        int? normalizedRows = maxRowsPerFile is > 0 ? maxRowsPerFile : null;
        long? normalizedBytes = maxBytesPerFile is > 0 ? maxBytesPerFile : null;

        if (!normalizedRows.HasValue && !normalizedBytes.HasValue)
        {
            return null;
        }

        logger.LogInformation(
            "Scheduled export job {ScheduledJobId} enabled file splitting with MaxRowsPerFile={MaxRowsPerFile}, MaxBytesPerFile={MaxBytesPerFile}.",
            scheduledJobId,
            normalizedRows,
            normalizedBytes);

        return new CsvExportSplitOptions
        {
            MaxRowsPerFile = normalizedRows,
            MaxBytesPerFile = normalizedBytes
        };
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

    private static string NormalizeSchemaTemplate(string? schemaTemplate)
            => schemaTemplate?.Trim() ?? string.Empty;
}
