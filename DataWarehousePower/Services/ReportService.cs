using DataWarehousePower.Models;
using DataWarehousePower.Repositories;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Spreadsheet;
using System.Globalization;
using System.Text.Json;

namespace DataWarehousePower.Services
{
    public class ReportService : IReportService
    {
        private const int MaxLabelLen = 100;
        private static readonly HashSet<string> _reservedFilterParameters = new(StringComparer.OrdinalIgnoreCase)
        {
            "ClientCode",
            "DateFrom",
            "DateTo",
            "FilterDateFrom",
            "FilterDateTo"
        };

        private static readonly string[] _dateFromParameterAliases = ["DateFrom", "FilterDateFrom"];
        private static readonly string[] _dateToParameterAliases = ["DateTo", "FilterDateTo"];

        private static readonly JsonSerializerOptions _jsonOpts =
            new() { PropertyNameCaseInsensitive = true };

        private static readonly string[] _clientCodeKeys = ["ClientCode", "clientCode", "ClintCode", "clintCode"];

        private readonly IReportRepository              _reportRepo;
        private readonly IColumnPreferenceRepository    _prefRepo;
        private readonly ILogger<ReportService>         _logger;

        public ReportService(
            IReportRepository reportRepo,
            IColumnPreferenceRepository prefRepo,
            ILogger<ReportService> logger)
        {
            _reportRepo = reportRepo;
            _prefRepo   = prefRepo;
            _logger     = logger;
        }

        public Task<List<ReportDefinition>> GetAllReportsAsync(string? userDepartment = null)
            => _reportRepo.GetAllReportsAsync(userDepartment);

        public Task<(List<ColumnDefinition> DisplayColumns, int? ActivePreferenceId)> LoadColumnPreferencesAsync(
            string userId,
            int reportId,
            string? schemaTemplate,
            IReadOnlyList<ColumnDefinition> systemColumns)
            => LoadPreferencesAsync(userId, reportId, NormalizeSchemaTemplate(schemaTemplate), systemColumns.ToList());

        public Task<(List<ColumnDefinition> DisplayColumns, int? ActivePreferenceId)> LoadDataFileColumnPreferencesAsync(
            string userId,
            int dataFileId,
            string? schemaTemplate,
            IReadOnlyList<ColumnDefinition> systemColumns)
            => LoadDataFilePreferencesAsync(userId, dataFileId, NormalizeSchemaTemplate(schemaTemplate), systemColumns.ToList());

        public Task<List<string>> GetSchemaTemplatesAsync(string userId, int reportId)
            => _prefRepo.GetSchemaTemplatesAsync(userId, reportId);

        public Task<List<string>> GetDataFileSchemaTemplatesAsync(string userId, int dataFileDefinitionId)
            => _prefRepo.GetDataFileSchemaTemplatesAsync(userId, dataFileDefinitionId);

        public Task<Dictionary<string, int>> GetSchemaTemplatePreferenceIdsAsync(string userId, int reportId)
            => _prefRepo.GetSchemaTemplatePreferenceIdsAsync(userId, reportId);

        public Task<Dictionary<string, int>> GetDataFileSchemaTemplatePreferenceIdsAsync(string userId, int dataFileDefinitionId)
            => _prefRepo.GetDataFileSchemaTemplatePreferenceIdsAsync(userId, dataFileDefinitionId);

        public async Task<ReportViewModel?> BuildReportViewModelAsync(
            int reportId,
            string userId,
            string? userDepartment = null,
            string? schemaTemplate = null,
            string? clientCode = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            IReadOnlyDictionary<string, string?>? parameterValues = null,
            bool loadData = true)
        {
            string normalizedSchemaTemplate = NormalizeSchemaTemplate(schemaTemplate);
            string? normalizedClientCode = NormalizeNullableClientCode(clientCode);
            var report = await _reportRepo.GetReportWithColumnsAsync(reportId);
            if (report is null) return null;

            List<ReportRuntimeParameter> runtimeParameters = new();
            Dictionary<string, string> activeParameterValues = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string?> spParameterValues = new(StringComparer.OrdinalIgnoreCase);
            bool hasMissingRequiredParameters = false;

            if (!string.IsNullOrWhiteSpace(report.SourceSP))
            {
                List<string> sourceParameterNames = await _reportRepo.GetStoredProcedureParameterNamesAsync(report.SourceSP);
                List<ReportParameterFormModel> configuredParameters = DeserializeConfiguredParameters(report.Parameters);
                spParameterValues = BuildStoredProcedureParameterValues(
                    sourceParameterNames,
                    configuredParameters,
                    parameterValues,
                    normalizedClientCode,
                    dateFrom,
                    dateTo);

                runtimeParameters = BuildRuntimeParameters(sourceParameterNames, configuredParameters, parameterValues);
                hasMissingRequiredParameters = runtimeParameters.Any(parameter =>
                    parameter.IsRequired && string.IsNullOrWhiteSpace(parameter.Value));

                activeParameterValues = runtimeParameters
                    .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Value))
                    .ToDictionary(
                        parameter => parameter.QueryKey,
                        parameter => parameter.Value!,
                        StringComparer.OrdinalIgnoreCase);
            }
            else if (!string.IsNullOrWhiteSpace(report.SourceTable))
            {
                runtimeParameters = BuildTableRuntimeParameters(report.Columns, parameterValues);
                activeParameterValues = runtimeParameters
                    .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Value))
                    .ToDictionary(
                        parameter => parameter.QueryKey,
                        parameter => parameter.Value!,
                        StringComparer.OrdinalIgnoreCase);
            }

            // Build system column list from DB definition
            var systemColumns = report.Columns
                .OrderBy(c => c.DisplayOrder)
                .Select(c => new ColumnDefinition
                {
                    Key          = c.PropertyName,
                    DefaultLabel = c.DefaultLabel,
                    DisplayLabel = c.DefaultLabel,
                    IsVisible    = true,
                    Order        = c.DisplayOrder
                })
                .ToList();

            // Apply user preferences
            (List<ColumnDefinition> displayColumns, int? activePreferenceId) =
                await LoadPreferencesAsync(userId, reportId, normalizedSchemaTemplate, systemColumns);

            // Fetch data — SP mode takes priority over table mode.
            List<Dictionary<string, object?>> rows;
            if (!loadData)
            {
                rows = new List<Dictionary<string, object?>>();
            }
            else if (!string.IsNullOrWhiteSpace(report.SourceSP))
            {
                if (hasMissingRequiredParameters)
                {
                    rows = new List<Dictionary<string, object?>>();
                }
                else
                {
                    // SP returns its own columns; the ReportColumns definition is used
                    // only for labelling/ordering in the UI — not for filtering SELECT columns
                    rows = await _reportRepo.GetReportDataFromSpAsync(
                        report.SourceSP,
                        normalizedClientCode,
                        dateFrom,
                        dateTo,
                        spParameterValues);
                }
            }
            else if (!string.IsNullOrWhiteSpace(report.SourceTable))
            {
                var columnNames = report.Columns.Select(c => c.PropertyName);

                rows = await _reportRepo.GetReportDataFromTableAsync(
                    report.SourceTable,
                    report.Columns,
                    report.SourceDatabase,
                    normalizedClientCode,
                    dateFrom,
                    dateTo);

                //rows = await _reportRepo.GetReportDataFromTableAsync(
                //    report.SourceTable,
                //    columnNames,
                //    report.SourceDatabase);

                //rows = ApplyMappedTableFilters(
                //    rows,
                //    report.Columns,
                //    normalizedClientCode,
                //    dateFrom,
                //    dateTo,
                //    activeParameterValues);
            }
            else
            {
                rows = new List<Dictionary<string, object?>>();
            }

            // All reports for sidebar navigation
            var allReports = await _reportRepo.GetAllReportsAsync(userDepartment);
            List<string> availableClientCodes = BuildAvailableClientCodes(
                normalizedClientCode,
                await _reportRepo.GetClientCodesByUserIdAsync(userId));
            List<string> savedSchemaTemplates = await _prefRepo.GetSchemaTemplatesAsync(userId, reportId);
            Dictionary<string, int> schemaTemplatePreferenceIds = await _prefRepo.GetSchemaTemplatePreferenceIdsAsync(userId, reportId);
            Dictionary<int, List<string>> reportSchemaTemplatesByReportId = await BuildReportSchemaTemplatesByReportIdAsync(userId, allReports);
            List<string> availableSchemaTemplates = BuildAvailableSchemaTemplates(
                normalizedSchemaTemplate,
                ExtractClientCodes(rows),
                savedSchemaTemplates);

            return new ReportViewModel
            {
                ReportId         = report.Id,
                ReportName       = report.ReportName,
                SchemaTemplate       = normalizedSchemaTemplate,
                ActivePreferenceId = activePreferenceId,
                ClientCode = normalizedClientCode ?? string.Empty,
                HasAppliedFilters = loadData,
                AvailableClientCodes = availableClientCodes,
                FilterDateFrom   = dateFrom,
                FilterDateTo     = dateTo,
                RuntimeParameters = runtimeParameters,
                HasMissingRequiredParameters = hasMissingRequiredParameters,
                ActiveParameterValues = activeParameterValues,
                AvailableSchemaTemplates = availableSchemaTemplates,
                SchemaTemplatePreferenceIds = schemaTemplatePreferenceIds,
                ReportSchemaTemplatesByReportId = reportSchemaTemplatesByReportId,
                AvailableColumns = systemColumns,
                DisplayColumns   = displayColumns,
                Rows             = rows,
                AllReports       = allReports
            };
        }

        public async Task<int> SaveDataFilePreferencesAsync(
            int dataFileId,
            string userId,
            string? clientCode,
            int? preferenceId,
            IEnumerable<SaveColumnRequest> columns,
            IReadOnlyList<ColumnDefinition> systemColumns)
        {
            string normalizedClientCode = NormalizeSchemaTemplate(clientCode);
            var validKeys = systemColumns.Select(c => c.Key)
                                         .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var entries = columns
                .Where(c => validKeys.Contains(c.Key))
                .Select(c =>
                {
                    var sys = systemColumns.First(s => s.Key.Equals(c.Key, StringComparison.OrdinalIgnoreCase));
                    var label = string.IsNullOrWhiteSpace(c.DisplayLabel)
                                    ? sys.DefaultLabel
                                    : c.DisplayLabel.Length > MaxLabelLen
                                        ? c.DisplayLabel[..MaxLabelLen]
                                        : c.DisplayLabel;

                    return new ColumnJsonEntry
                    {
                        PropertyName = c.Key,
                        IsVisible = c.IsVisible,
                        DisplayOrder = Math.Max(1, c.Order),
                        CustomName = label
                    };
                })
                .ToList();

            return await _prefRepo.UpsertAsync(new UserColumnPreference
            {
                UserId = userId,
                DataFileDefinitionId = dataFileId,
                SchemaTemplate = normalizedClientCode,
                ColumnJson = JsonSerializer.Serialize(entries)
            }, preferenceId);
        }

        public async Task<int> SavePreferencesAsync(
            int reportId,
            string userId,
            string? clientCode,
            int? preferenceId,
            IEnumerable<SaveColumnRequest> columns,
            IReadOnlyList<ColumnDefinition> systemColumns)
        {
            string normalizedClientCode = NormalizeSchemaTemplate(clientCode);
            var validKeys = systemColumns.Select(c => c.Key)
                                         .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var entries = columns
                .Where(c => validKeys.Contains(c.Key))
                .Select(c =>
                {
                    var sys   = systemColumns.First(s => s.Key.Equals(c.Key, StringComparison.OrdinalIgnoreCase));
                    var label = string.IsNullOrWhiteSpace(c.DisplayLabel)
                                    ? sys.DefaultLabel
                                    : c.DisplayLabel.Length > MaxLabelLen
                                        ? c.DisplayLabel[..MaxLabelLen]
                                        : c.DisplayLabel;

                    return new ColumnJsonEntry
                    {
                        PropertyName = c.Key,
                        IsVisible    = c.IsVisible,
                        DisplayOrder = Math.Max(1, c.Order),
                        CustomName   = label
                    };
                })
                .ToList();

            return await _prefRepo.UpsertAsync(new UserColumnPreference
            {
                UserId             = userId,
                ReportDefinitionId = reportId,
                SchemaTemplate         = normalizedClientCode,
                ColumnJson         = JsonSerializer.Serialize(entries)
            }, preferenceId);
        }

        public Task UpdateSchemaTemplateAsync(int reportId, string userId, int preferenceId, string newSchemaTemplate)
            => _prefRepo.UpdateSchemaTemplateAsync(userId, reportId, preferenceId, newSchemaTemplate);

        public Task DeletePreferenceAsync(int reportId, string userId, int preferenceId)
            => _prefRepo.DeleteAsync(userId, reportId, preferenceId);

        // ── Private helpers ───────────────────────────────────────────────────

        private async Task<(List<ColumnDefinition> Columns, int? PreferenceId)> LoadPreferencesAsync(
            string userId,
            int reportId,
            string schemaTemplate,
            List<ColumnDefinition> systemColumns)
        {
            try
            {
                var row = await _prefRepo.GetAsync(userId, reportId, schemaTemplate);
                if (row is null) return (BuildDefaults(systemColumns), null);

                var entries = JsonSerializer.Deserialize<List<ColumnJsonEntry>>(row.ColumnJson, _jsonOpts)
                              ?? new List<ColumnJsonEntry>();

                var entryLookup = entries
                    .Where(e => !string.IsNullOrWhiteSpace(e.PropertyName))
                    .GroupBy(e => e.PropertyName, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                var mergedColumns = systemColumns
                    .OrderBy(c => c.Order)
                    .Select(c =>
                    {
                        bool hasSavedEntry = entryLookup.TryGetValue(c.Key, out ColumnJsonEntry? savedEntry);
                        bool isVisible = hasSavedEntry ? savedEntry!.IsVisible : true;
                        string displayLabel = hasSavedEntry && !string.IsNullOrWhiteSpace(savedEntry!.CustomName)
                            ? savedEntry.CustomName
                            : c.DefaultLabel;
                        int order = hasSavedEntry
                            ? Math.Max(1, savedEntry!.DisplayOrder)
                            : c.Order;

                        return new ColumnDefinition
                        {
                            Key = c.Key,
                            DefaultLabel = c.DefaultLabel,
                            DisplayLabel = displayLabel,
                            IsVisible = isVisible,
                            Order = order
                        };
                    })
                    .ToList();

                return (mergedColumns, row.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to load preferences for user {UserId} report {ReportId} client code {ClientCode}. Using defaults.",
                    userId, reportId, schemaTemplate);
                return (BuildDefaults(systemColumns), null);
            }
        }

        private async Task<(List<ColumnDefinition> Columns, int? PreferenceId)> LoadDataFilePreferencesAsync(
            string userId,
            int dataFileId,
            string schemaTemplate,
            List<ColumnDefinition> systemColumns)
        {
            try
            {
                var row = await _prefRepo.GetDataFileAsync(userId, dataFileId, schemaTemplate);
                if (row is null) return (BuildDefaults(systemColumns), null);

                var entries = JsonSerializer.Deserialize<List<ColumnJsonEntry>>(row.ColumnJson, _jsonOpts)
                              ?? new List<ColumnJsonEntry>();

                var entryLookup = entries
                    .Where(e => !string.IsNullOrWhiteSpace(e.PropertyName))
                    .GroupBy(e => e.PropertyName, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                var mergedColumns = systemColumns
                    .OrderBy(c => c.Order)
                    .Select(c =>
                    {
                        bool hasSavedEntry = entryLookup.TryGetValue(c.Key, out ColumnJsonEntry? savedEntry);
                        bool isVisible = hasSavedEntry ? savedEntry!.IsVisible : true;
                        string displayLabel = hasSavedEntry && !string.IsNullOrWhiteSpace(savedEntry!.CustomName)
                            ? savedEntry.CustomName
                            : c.DefaultLabel;
                        int order = hasSavedEntry
                            ? Math.Max(1, savedEntry!.DisplayOrder)
                            : c.Order;

                        return new ColumnDefinition
                        {
                            Key = c.Key,
                            DefaultLabel = c.DefaultLabel,
                            DisplayLabel = displayLabel,
                            IsVisible = isVisible,
                            Order = order
                        };
                    })
                    .ToList();

                return (mergedColumns, row.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to load preferences for user {UserId} report {ReportId} client code {ClientCode}. Using defaults.",
                    userId, dataFileId, schemaTemplate);
                return (BuildDefaults(systemColumns), null);
            }
        }

        private static string NormalizeSchemaTemplate(string? schemaTemplate)
            => schemaTemplate?.Trim() ?? string.Empty;

        private static string? NormalizeNullableClientCode(string? clientCode)
        {
            string normalized = clientCode?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
        }

        private static List<string> BuildAvailableSchemaTemplates(
            string currentClientCode,
            IEnumerable<string> rowClientCodes,
            IEnumerable<string> savedClientCodes)
            => rowClientCodes
                .Concat(savedClientCodes)
                .Append(currentClientCode)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();

        private static List<string> BuildAvailableClientCodes(
            string? currentClientCode,
            IEnumerable<string> clientCodesByUserId)
            => clientCodesByUserId
                .Append(currentClientCode ?? string.Empty)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();

        private static List<string> ExtractClientCodes(IEnumerable<Dictionary<string, object?>> rows)
            => rows.Select(FindClientCodeValue)
                   .Where(value => !string.IsNullOrWhiteSpace(value))
                   .Select(value => value!)
                   .Distinct(StringComparer.OrdinalIgnoreCase)
                   .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                   .ToList();

        private async Task<Dictionary<int, List<string>>> BuildReportSchemaTemplatesByReportIdAsync(
            string userId,
            IEnumerable<ReportDefinition> reports)
        {
            Dictionary<int, List<string>> schemaTemplatesByReportId = new();

            foreach (ReportDefinition report in reports)
            {
                schemaTemplatesByReportId[report.Id] = await _prefRepo.GetSchemaTemplatesAsync(userId, report.Id);
            }

            return schemaTemplatesByReportId;
        }

        private static string? FindClientCodeValue(Dictionary<string, object?> row)
        {
            foreach (string clientCodeKey in _clientCodeKeys)
            {
                KeyValuePair<string, object?>? match = row.FirstOrDefault(entry =>
                    entry.Key.Equals(clientCodeKey, StringComparison.OrdinalIgnoreCase));

                if (match.HasValue && match.Value.Value is not null)
                {
                    string clientCode = match.Value.Value.ToString()?.Trim() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(clientCode))
                    {
                        return clientCode;
                    }
                }
            }

            return null;
        }

        private static List<ColumnDefinition> BuildDefaults(List<ColumnDefinition> systemColumns)
            => systemColumns.Select((c, i) => new ColumnDefinition
            {
                Key          = c.Key,
                DefaultLabel = c.DefaultLabel,
                DisplayLabel = c.DefaultLabel,
                IsVisible    = true,
                Order        = i + 1
            }).ToList();

        private static List<ReportRuntimeParameter> BuildRuntimeParameters(
            IEnumerable<string> sourceParameterNames,
            IEnumerable<ReportParameterFormModel> configuredParameters,
            IReadOnlyDictionary<string, string?>? requestParameterValues)
        {
            Dictionary<string, ReportParameterFormModel> configuredByName = configuredParameters
                .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Name))
                .GroupBy(parameter => NormalizeParameterName(parameter.Name), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            List<ReportRuntimeParameter> runtimeParameters = new();
            foreach (string sourceParameterName in sourceParameterNames)
            {
                string normalizedName = NormalizeParameterName(sourceParameterName);
                configuredByName.TryGetValue(normalizedName, out ReportParameterFormModel? configured);
                string mappedQueryKey = NormalizeMappedParameterKey(configured?.MappingParameter, normalizedName);

                if (_reservedFilterParameters.Contains(mappedQueryKey))
                {
                    continue;
                }

                string? defaultValue = configured?.DefaultValue?.Trim();
                string? requestValue = ResolveRequestParameterValue(requestParameterValues, mappedQueryKey);
                string? effectiveValue = !string.IsNullOrWhiteSpace(requestValue) ? requestValue : defaultValue;

                runtimeParameters.Add(new ReportRuntimeParameter
                {
                    Name = "@" + normalizedName,
                    QueryKey = mappedQueryKey,
                    DefaultValue = defaultValue,
                    Value = effectiveValue,
                    IsRequired = true
                });
            }

            return runtimeParameters;
        }

        private static Dictionary<string, string?> BuildStoredProcedureParameterValues(
            IEnumerable<string> sourceParameterNames,
            IEnumerable<ReportParameterFormModel> configuredParameters,
            IReadOnlyDictionary<string, string?>? requestParameterValues,
            string? clientCode,
            DateTime? dateFrom,
            DateTime? dateTo)
        {
            Dictionary<string, ReportParameterFormModel> configuredByName = configuredParameters
                .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Name))
                .GroupBy(parameter => NormalizeParameterName(parameter.Name), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            Dictionary<string, string?> result = new(StringComparer.OrdinalIgnoreCase);
            foreach (string sourceParameterName in sourceParameterNames)
            {
                string normalizedName = NormalizeParameterName(sourceParameterName);
                configuredByName.TryGetValue(normalizedName, out ReportParameterFormModel? configured);

                string mappedQueryKey = NormalizeMappedParameterKey(configured?.MappingParameter, normalizedName);
                string? value = ResolveMappedSystemFilterValue(mappedQueryKey, clientCode, dateFrom, dateTo);

                if (string.IsNullOrWhiteSpace(value))
                {
                    value = ResolveRequestParameterValue(requestParameterValues, mappedQueryKey);
                }

                if (string.IsNullOrWhiteSpace(value))
                {
                    value = configured?.DefaultValue?.Trim();
                }

                result["@" + normalizedName] = value;
            }

            return result;
        }

        private static string NormalizeMappedParameterKey(string? mappingParameter, string fallback)
        {
            string normalized = NormalizeFilterParameterAlias(NormalizeParameterName(mappingParameter ?? string.Empty));
            return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
        }

        private static string? ResolveMappedSystemFilterValue(
            string mappedQueryKey,
            string? clientCode,
            DateTime? dateFrom,
            DateTime? dateTo)
        {
            if (mappedQueryKey.Equals("ClientCode", StringComparison.OrdinalIgnoreCase))
            {
                return clientCode;
            }

            if (mappedQueryKey.Equals("DateFrom", StringComparison.OrdinalIgnoreCase))
            {
                return dateFrom?.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            if (mappedQueryKey.Equals("DateTo", StringComparison.OrdinalIgnoreCase))
            {
                return dateTo?.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            return null;
        }

        private static List<ReportRuntimeParameter> BuildTableRuntimeParameters(
            IEnumerable<ReportColumn> reportColumns,
            IReadOnlyDictionary<string, string?>? requestParameterValues)
            => reportColumns
                .SelectMany(column => GetMappingParameters(column.MappingParameter))
                .Where(parameterName => !_reservedFilterParameters.Contains(parameterName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(parameterName => parameterName, StringComparer.OrdinalIgnoreCase)
                .Select(parameterName => new ReportRuntimeParameter
                {
                    Name = parameterName,
                    QueryKey = parameterName,
                    Value = ResolveRequestParameterValue(requestParameterValues, parameterName),
                    IsRequired = false
                })
                .ToList();

        private static List<Dictionary<string, object?>> ApplyMappedTableFilters(
            List<Dictionary<string, object?>> rows,
            IEnumerable<ReportColumn> reportColumns,
            string? clientCode,
            DateTime? dateFrom,
            DateTime? dateTo,
            IReadOnlyDictionary<string, string> parameterValues)
        {
            Dictionary<string, List<string>> columnsByParameter = reportColumns
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
                TryGetMappedColumns(columnsByParameter, _dateFromParameterAliases, out List<string> dateFromColumns))
            {
                //filteredRows = filteredRows.Where(row => RowMatchesMappedDate(row, dateFromColumns, dateFrom.Value.Date, isLowerBound: true));
                filteredRows = filteredRows.Where(row => RowMatchesMappedDate(row, dateFromColumns, dateFrom.Value, isLowerBound: true));
            }

            if (dateTo.HasValue &&
                TryGetMappedColumns(columnsByParameter, _dateToParameterAliases, out List<string> dateToColumns))
            {
                filteredRows = filteredRows.Where(row => RowMatchesMappedDate(row, dateToColumns, dateTo.Value, isLowerBound: false));
            }

            foreach ((string parameterName, string parameterValue) in parameterValues)
            {
                if (!columnsByParameter.TryGetValue(parameterName, out List<string>? mappedColumns))
                {
                    continue;
                }

                filteredRows = filteredRows.Where(row => RowMatchesMappedValue(row, mappedColumns, parameterValue));
            }

            return filteredRows.ToList();
        }

        private static bool TryGetMappedColumns(
            IReadOnlyDictionary<string, List<string>> columnsByParameter,
            IEnumerable<string> aliases,
            out List<string> mappedColumns)
        {
            List<string> combinedColumns = new();

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
                .Select(parameterName => NormalizeFilterParameterAlias(parameterName))
                .Where(parameterName => !string.IsNullOrWhiteSpace(parameterName));

        private static string NormalizeFilterParameterAlias(string parameterName)
        {
            string normalized = NormalizeParameterName(parameterName);
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

                string actualValue = Convert.ToString(rawValue, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
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

                //if (isLowerBound ? valueDate.Date >= boundary : valueDate.Date <= boundary)
                if (isLowerBound ? valueDate >= boundary : valueDate <= boundary)
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
                case null:
                    valueDate = default;
                    return false;
                default:
                    return DateTime.TryParse(
                        Convert.ToString(rawValue, CultureInfo.InvariantCulture),
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AllowWhiteSpaces,
                        out valueDate);
            }
        }

        private static List<ReportParameterFormModel> DeserializeConfiguredParameters(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new();
            }

            try
            {
                return JsonSerializer.Deserialize<List<ReportParameterFormModel>>(json, _jsonOpts) ?? new();
            }
            catch
            {
                return new();
            }
        }

        private static string NormalizeParameterName(string name)
            => name.Trim().TrimStart('@');

        private static string? ResolveRequestParameterValue(
            IReadOnlyDictionary<string, string?>? requestParameterValues,
            string normalizedParameterName)
        {
            if (requestParameterValues is null)
            {
                return null;
            }

            if (requestParameterValues.TryGetValue(normalizedParameterName, out string? value))
            {
                return value?.Trim();
            }

            if (requestParameterValues.TryGetValue("@" + normalizedParameterName, out value))
            {
                return value?.Trim();
            }

            return null;
        }
    }
}
