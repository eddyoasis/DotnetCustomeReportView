using DataWarehousePower.Models;

namespace DataWarehousePower.Repositories
{
    public interface IColumnPreferenceRepository
    {
        Task<UserColumnPreference?> GetAsync(string userId, int reportDefinitionId, string? clientCode);
        Task<UserColumnPreference?> GetByIdAsync(string userId, int reportDefinitionId, int preferenceId);
        Task<List<string>> GetClientCodesAsync(string userId, int reportDefinitionId);
        Task<Dictionary<string, int>> GetClientCodePreferenceIdsAsync(string userId, int reportDefinitionId);
        Task<int> UpsertAsync(UserColumnPreference preference, int? preferenceId = null);
        Task UpdateClientCodeAsync(string userId, int reportDefinitionId, int preferenceId, string newClientCode);
        Task DeleteAsync(string userId, int reportDefinitionId, int preferenceId);
    }
}
