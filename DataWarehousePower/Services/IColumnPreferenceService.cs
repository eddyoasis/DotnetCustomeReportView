using DataWarehousePower.Models;

namespace DataWarehousePower.Services
{
    public interface IColumnPreferenceService
    {
        /// <summary>
        /// Resolves or creates a UserId from the current HTTP context cookie.
        /// </summary>
        string ResolveUserId(HttpContext httpContext);

        /// <summary>
        /// Loads saved preferences for the given user and merges them with
        /// the system-defined column list. Falls back to defaults if records
        /// are missing or incomplete.
        /// </summary>
        Task<List<ColumnDefinition>> LoadPreferencesAsync(string userId,
            IReadOnlyList<ColumnDefinition> systemColumns);

        /// <summary>
        /// Persists the user's column preferences atomically.
        /// </summary>
        Task SavePreferencesAsync(string userId,
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
