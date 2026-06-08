using DataWarehousePower.Models;

namespace DataWarehousePower.Repositories
{
    public interface IColumnPreferenceRepository
    {
        /// <summary>Returns the single preference row for the user, or null if none exists.</summary>
        Task<UserColumnPreference?> GetByUserIdAsync(string userId);

        /// <summary>Inserts or replaces the preference row for the user.</summary>
        Task UpsertAsync(UserColumnPreference preference);
    }
}
