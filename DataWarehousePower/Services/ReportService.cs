using DataWarehousePower.Models;
using DataWarehousePower.Repositories;
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
            "DateTo"
        };

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

        public async Task<ReportViewModel?> BuildReportViewModelAsync(
            int reportId,
            string userId,
            string? userDepartment = null,
            string? schemaTemplate = null,
            string? clientCode = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            IReadOnlyDictionary<string, string?>? parameterValues = null)
        {
            string normalizedSchemaTemplate = NormalizeSchemaTemplate(schemaTemplate);
            string? normalizedClientCode = NormalizeNullableClientCode(clientCode);
            var report = await _reportRepo.GetReportWithColumnsAsync(reportId, userDepartment);
            if (report is null) return null;

            List<ReportRuntimeParameter> runtimeParameters = new();
            Dictionary<string, string> activeParameterValues = new(StringComparer.OrdinalIgnoreCase);
            bool hasMissingRequiredParameters = false;

            if (!string.IsNullOrWhiteSpace(report.SourceSP))
            {
                List<string> sourceParameterNames = await _reportRepo.GetStoredProcedureParameterNamesAsync(report.SourceSP);
                List<ReportParameterFormModel> configuredParameters = DeserializeConfiguredParameters(report.Parameters);

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

            // Fetch data — SP mode takes priority over table mode
            List<Dictionary<string, object?>> rows;
            if (!string.IsNullOrWhiteSpace(report.SourceSP))
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
                        runtimeParameters.ToDictionary(
                            parameter => parameter.Name,
                            parameter => parameter.Value,
                            StringComparer.OrdinalIgnoreCase));
                }
            }
            else if (!string.IsNullOrWhiteSpace(report.SourceTable))
            {
                var columnNames = report.Columns.Select(c => c.PropertyName);
                rows = await _reportRepo.GetReportDataFromTableAsync(
                    report.SourceTable,
                    columnNames,
                    report.SourceDatabase);
            }
            else
            {
                rows = new List<Dictionary<string, object?>>();
            }

            // All reports for sidebar navigation
            var allReports = await _reportRepo.GetAllReportsAsync(userDepartment);
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

                        return new ColumnDefinition
                        {
                            Key = c.Key,
                            DefaultLabel = c.DefaultLabel,
                            DisplayLabel = displayLabel,
                            IsVisible = isVisible,
                            Order = c.Order
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
                if (_reservedFilterParameters.Contains(normalizedName))
                {
                    continue;
                }

                configuredByName.TryGetValue(normalizedName, out ReportParameterFormModel? configured);

                string? defaultValue = configured?.DefaultValue?.Trim();
                string? requestValue = ResolveRequestParameterValue(requestParameterValues, normalizedName);
                string? effectiveValue = !string.IsNullOrWhiteSpace(requestValue) ? requestValue : defaultValue;

                runtimeParameters.Add(new ReportRuntimeParameter
                {
                    Name = "@" + normalizedName,
                    QueryKey = normalizedName,
                    DefaultValue = defaultValue,
                    Value = effectiveValue,
                    IsRequired = true
                });
            }

            return runtimeParameters;
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
