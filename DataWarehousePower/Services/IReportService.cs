using DataWarehousePower.Models;

namespace DataWarehousePower.Services
{
    public interface IReportService
    {
        Task<List<ReportDefinition>> GetAllReportsAsync(string? userDepartment = null);

        /// <summary>
        /// Builds the complete ReportViewModel for the given report:
        /// loads the report definition, applies user preferences, and fetches data.
        /// Returns null if the reportId does not exist.
        /// </summary>
        Task<ReportViewModel?> BuildReportViewModelAsync(
            int reportId,
            string userId,
            string? userDepartment = null,
            string? schemaTemplate = null,
            string? clientCode = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            IReadOnlyDictionary<string, string?>? parameterValues = null);

        /// <summary>
        /// Persists the user's column preferences for a specific report.
        /// </summary>
        Task<int> SavePreferencesAsync(int reportId, string userId, string? schemaTemplate,
            int? preferenceId,
            IEnumerable<SaveColumnRequest> columns,
            IReadOnlyList<ColumnDefinition> systemColumns);

        Task UpdateSchemaTemplateAsync(int reportId, string userId, int preferenceId, string newSchemaTemplate);
        Task DeletePreferenceAsync(int reportId, string userId, int preferenceId);
    }
}
