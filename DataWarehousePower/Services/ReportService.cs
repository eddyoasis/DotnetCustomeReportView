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

        public async Task<ReportViewModel?> BuildReportViewModelAsync(int reportId, string userId)
        {
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
            var displayColumns = await LoadPreferencesAsync(userId, reportId, systemColumns);

            // Fetch data — SP mode takes priority over table mode
            List<Dictionary<string, object?>> rows;
            if (!string.IsNullOrWhiteSpace(report.SourceSP))
            {
                // SP returns its own columns; the ReportColumns definition is used
                // only for labelling/ordering in the UI — not for filtering SELECT columns
                rows = await _reportRepo.GetReportDataFromSpAsync(report.SourceSP);
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

            return new ReportViewModel
            {
                ReportId         = report.Id,
                ReportName       = report.ReportName,
                AvailableColumns = systemColumns,
                DisplayColumns   = displayColumns,
                Rows             = rows,
                AllReports       = allReports
            };
        }

        public async Task SavePreferencesAsync(
            int reportId,
            string userId,
            IEnumerable<SaveColumnRequest> columns,
            IReadOnlyList<ColumnDefinition> systemColumns)
        {
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

            await _prefRepo.UpsertAsync(new UserColumnPreference
            {
                UserId             = userId,
                ReportDefinitionId = reportId,
                ColumnJson         = JsonSerializer.Serialize(entries)
            });
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private async Task<List<ColumnDefinition>> LoadPreferencesAsync(
            string userId,
            int reportId,
            List<ColumnDefinition> systemColumns)
        {
            try
            {
                var row = await _prefRepo.GetAsync(userId, reportId);
                if (row is null) return BuildDefaults(systemColumns);

                var entries = JsonSerializer.Deserialize<List<ColumnJsonEntry>>(row.ColumnJson, _jsonOpts)
                              ?? new List<ColumnJsonEntry>();

                var validKeys    = systemColumns.Select(c => c.Key).ToHashSet();
                var validEntries = entries.Where(e => validKeys.Contains(e.PropertyName)).ToList();

                if (!systemColumns.All(c => validEntries.Any(e => e.PropertyName == c.Key)))
                    return BuildDefaults(systemColumns);

                return validEntries
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
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to load preferences for user {UserId} report {ReportId}. Using defaults.",
                    userId, reportId);
                return BuildDefaults(systemColumns);
            }
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
