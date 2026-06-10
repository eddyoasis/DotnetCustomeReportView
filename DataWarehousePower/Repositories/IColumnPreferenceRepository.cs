using DataWarehousePower.Models;

namespace DataWarehousePower.Repositories
{
    public interface IColumnPreferenceRepository
    {
        Task<UserColumnPreference?> GetAsync(string userId, int reportDefinitionId, string? clientCode);
        Task<List<string>> GetClientCodesAsync(string userId, int reportDefinitionId);
        Task UpsertAsync(UserColumnPreference preference);
    }
}
