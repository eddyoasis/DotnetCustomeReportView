using DataWarehousePower.Models;

namespace DataWarehousePower.Services
{
    public interface IReportService
    {
        Task<List<ReportDefinition>> GetAllReportsAsync(string? userDepartment = null);

        Task<(List<ColumnDefinition> DisplayColumns, int? ActivePreferenceId)> LoadColumnPreferencesAsync(
            string userId,
            int reportId,
            string? schemaTemplate,
            IReadOnlyList<ColumnDefinition> systemColumns);

        Task<(List<ColumnDefinition> DisplayColumns, int? ActivePreferenceId)> LoadDataFileColumnPreferencesAsync(
            string userId,
            int dataFileId,
            string? schemaTemplate,
            IReadOnlyList<ColumnDefinition> systemColumns);

        Task<List<string>> GetClientCodesByUserIdAsync(string userId);

        Task<List<string>> GetClientCodeFilterValuesAsync(int reportId, string userId, string? search = null, int take = 50);

        Task<List<string>> GetDataFileSchemaTemplatesAsync(string userId, int dataFileDefinitionId);

        Task<List<string>> GetSchemaTemplatesAsync(string userId, int reportId);

        Task<Dictionary<string, int>> GetDataFileSchemaTemplatePreferenceIdsAsync(string userId, int dataFileDefinitionId);

        Task<Dictionary<string, int>> GetSchemaTemplatePreferenceIdsAsync(string userId, int reportId);

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
            IReadOnlyDictionary<string, string?>? parameterValues = null,
            bool loadData = true);

        /// <summary>
        /// Persists the user's column preferences for a specific report.
        /// </summary>
        Task<int> SavePreferencesAsync(int reportId, string userId, string? schemaTemplate,
            int? preferenceId,
            IEnumerable<SaveColumnRequest> columns,
            IReadOnlyList<ColumnDefinition> systemColumns);

        Task<int> SaveDataFilePreferencesAsync(
            int dataFileId,
            string userId,
            string? clientCode,
            int? preferenceId,
            IEnumerable<SaveColumnRequest> columns,
            IReadOnlyList<ColumnDefinition> systemColumns);

        Task UpdateSchemaTemplateAsync(int reportId, string userId, int preferenceId, string newSchemaTemplate);
        Task DeletePreferenceAsync(int reportId, string userId, int preferenceId);
    }
}
