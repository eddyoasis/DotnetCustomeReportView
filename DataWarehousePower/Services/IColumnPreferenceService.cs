using DataWarehousePower.Models;

namespace DataWarehousePower.Services
{
    public interface IColumnPreferenceService
    {
        string ResolveUserId(HttpContext httpContext);

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

        Task<List<string>> GetDataFileSchemaTemplatesAsync(string userId, int dataFileDefinitionId);

        Task<List<string>> GetSchemaTemplatesAsync(string userId, int reportId);

        Task<Dictionary<string, int>> GetDataFileSchemaTemplatePreferenceIdsAsync(string userId, int dataFileDefinitionId);

        Task<Dictionary<string, int>> GetSchemaTemplatePreferenceIdsAsync(string userId, int reportId);

        // Kept for direct use by the controller — delegates to ReportService internally
        Task<int> SavePreferencesAsync(string userId, int reportId, string? clientCode,
            int? preferenceId,
            IEnumerable<SaveColumnRequest> columns,
            IReadOnlyList<ColumnDefinition> systemColumns);

        Task UpdateSchemaTemplateAsync(string userId, int reportId, int preferenceId, string newSchemaTemplate);
        Task DeletePreferenceAsync(string userId, int reportId, int preferenceId);
    }

    public class SaveColumnRequest
    {
        public string Key          { get; set; } = string.Empty;
        public bool   IsVisible    { get; set; }
        public int    Order        { get; set; }
        public string DisplayLabel { get; set; } = string.Empty;
    }
}
