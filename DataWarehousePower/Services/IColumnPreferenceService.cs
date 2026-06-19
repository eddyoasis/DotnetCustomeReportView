using DataWarehousePower.Models;

namespace DataWarehousePower.Services
{
    public interface IColumnPreferenceService
    {
        string ResolveUserId(HttpContext httpContext);

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
