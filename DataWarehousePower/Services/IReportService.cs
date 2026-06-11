using DataWarehousePower.Models;

namespace DataWarehousePower.Services
{
    public interface IReportService
    {
        Task<List<ReportDefinition>> GetAllReportsAsync();

        /// <summary>
        /// Builds the complete ReportViewModel for the given report:
        /// loads the report definition, applies user preferences, and fetches data.
        /// Returns null if the reportId does not exist.
        /// </summary>
        Task<ReportViewModel?> BuildReportViewModelAsync(
            int reportId,
            string userId,
            string? clientCode = null,
            string? filterClientCode = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null);

        /// <summary>
        /// Persists the user's column preferences for a specific report.
        /// </summary>
        Task<int> SavePreferencesAsync(int reportId, string userId, string? clientCode,
            int? preferenceId,
            IEnumerable<SaveColumnRequest> columns,
            IReadOnlyList<ColumnDefinition> systemColumns);

        Task UpdateClientCodeAsync(int reportId, string userId, int preferenceId, string newClientCode);
        Task DeletePreferenceAsync(int reportId, string userId, int preferenceId);
    }
}
