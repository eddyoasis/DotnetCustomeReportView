using DataWarehousePower.Models;

namespace DataWarehousePower.Services
{
    public interface IColumnPreferenceService
    {
        string ResolveUserId(HttpContext httpContext);

        // Kept for direct use by the controller — delegates to ReportService internally
        Task SavePreferencesAsync(string userId, int reportId, string? clientCode,
            IEnumerable<SaveColumnRequest> columns,
            IReadOnlyList<ColumnDefinition> systemColumns);
    }

    public class SaveColumnRequest
    {
        public string Key          { get; set; } = string.Empty;
        public bool   IsVisible    { get; set; }
        public int    Order        { get; set; }
        public string DisplayLabel { get; set; } = string.Empty;
    }
}
