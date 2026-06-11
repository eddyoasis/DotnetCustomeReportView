using DataWarehousePower.Models;
using DataWarehousePower.Repositories;
using System.Text.Json;

namespace DataWarehousePower.Services
{
    public class ReportService : IReportService
    {
        private const int MaxLabelLen = 100;

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

        public Task<List<ReportDefinition>> GetAllReportsAsync()
            => _reportRepo.GetAllReportsAsync();

        public async Task<ReportViewModel?> BuildReportViewModelAsync(
            int reportId,
            string userId,
            string? clientCode = null,
            string? filterClientCode = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null)
        {
            string normalizedClientCode = NormalizeClientCode(clientCode);
            string normalizedFilterClientCode = NormalizeNullableClientCode(filterClientCode);
            var report = await _reportRepo.GetReportWithColumnsAsync(reportId);
            if (report is null) return null;

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
                await LoadPreferencesAsync(userId, reportId, normalizedClientCode, systemColumns);

            // Fetch data — SP mode takes priority over table mode
            List<Dictionary<string, object?>> rows;
            if (!string.IsNullOrWhiteSpace(report.SourceSP))
            {
                // SP returns its own columns; the ReportColumns definition is used
                // only for labelling/ordering in the UI — not for filtering SELECT columns
                rows = await _reportRepo.GetReportDataFromSpAsync(
                    report.SourceSP,
                    normalizedFilterClientCode,
                    dateFrom,
                    dateTo);
            }
            else if (!string.IsNullOrWhiteSpace(report.SourceTable))
            {
                var columnNames = report.Columns.Select(c => c.PropertyName);
                rows = await _reportRepo.GetReportDataFromTableAsync(report.SourceTable, columnNames);
            }
            else
            {
                rows = new List<Dictionary<string, object?>>();
            }

            // All reports for sidebar navigation
            var allReports = await _reportRepo.GetAllReportsAsync();
            List<string> savedClientCodes = await _prefRepo.GetClientCodesAsync(userId, reportId);
            Dictionary<string, int> clientCodePreferenceIds = await _prefRepo.GetClientCodePreferenceIdsAsync(userId, reportId);
            List<string> availableClientCodes = BuildAvailableClientCodes(
                normalizedClientCode,
                ExtractClientCodes(rows),
                savedClientCodes);

            return new ReportViewModel
            {
                ReportId         = report.Id,
                ReportName       = report.ReportName,
                ClientCode       = normalizedClientCode,
                ActivePreferenceId = activePreferenceId,
                FilterClientCode = normalizedFilterClientCode ?? string.Empty,
                FilterDateFrom   = dateFrom,
                FilterDateTo     = dateTo,
                AvailableClientCodes = availableClientCodes,
                ClientCodePreferenceIds = clientCodePreferenceIds,
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
            string normalizedClientCode = NormalizeClientCode(clientCode);
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
                ClientCode         = normalizedClientCode,
                ColumnJson         = JsonSerializer.Serialize(entries)
            }, preferenceId);
        }

        public Task UpdateClientCodeAsync(int reportId, string userId, int preferenceId, string newClientCode)
            => _prefRepo.UpdateClientCodeAsync(userId, reportId, preferenceId, newClientCode);

        public Task DeletePreferenceAsync(int reportId, string userId, int preferenceId)
            => _prefRepo.DeleteAsync(userId, reportId, preferenceId);

        // ── Private helpers ───────────────────────────────────────────────────

        private async Task<(List<ColumnDefinition> Columns, int? PreferenceId)> LoadPreferencesAsync(
            string userId,
            int reportId,
            string clientCode,
            List<ColumnDefinition> systemColumns)
        {
            try
            {
                var row = await _prefRepo.GetAsync(userId, reportId, clientCode);
                if (row is null) return (BuildDefaults(systemColumns), null);

                var entries = JsonSerializer.Deserialize<List<ColumnJsonEntry>>(row.ColumnJson, _jsonOpts)
                              ?? new List<ColumnJsonEntry>();

                var validKeys    = systemColumns.Select(c => c.Key).ToHashSet();
                var validEntries = entries.Where(e => validKeys.Contains(e.PropertyName)).ToList();

                if (!systemColumns.All(c => validEntries.Any(e => e.PropertyName == c.Key)))
                    return (BuildDefaults(systemColumns), row.Id);

                return (validEntries
                    .OrderBy(e => e.DisplayOrder)
                    .Select(e =>
                    {
                        var sys = systemColumns.First(c => c.Key == e.PropertyName);
                        return new ColumnDefinition
                        {
                            Key          = e.PropertyName,
                            DefaultLabel = sys.DefaultLabel,
                            DisplayLabel = string.IsNullOrWhiteSpace(e.CustomName)
                                               ? sys.DefaultLabel : e.CustomName,
                            IsVisible    = e.IsVisible,
                            Order        = e.DisplayOrder
                        };
                    })
                    .ToList(), row.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to load preferences for user {UserId} report {ReportId} client code {ClientCode}. Using defaults.",
                    userId, reportId, clientCode);
                return (BuildDefaults(systemColumns), null);
            }
        }

        private static string NormalizeClientCode(string? clientCode)
            => clientCode?.Trim() ?? string.Empty;

        private static string? NormalizeNullableClientCode(string? clientCode)
        {
            string normalized = clientCode?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
        }

        private static List<string> BuildAvailableClientCodes(
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
    }
}
