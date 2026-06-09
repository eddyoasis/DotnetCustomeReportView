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
        Task<ReportViewModel?> BuildReportViewModelAsync(int reportId, string userId);

        /// <summary>
        /// Persists the user's column preferences for a specific report.
        /// </summary>
        Task SavePreferencesAsync(int reportId, string userId,
            IEnumerable<SaveColumnRequest> columns,
            IReadOnlyList<ColumnDefinition> systemColumns);
    }
}
