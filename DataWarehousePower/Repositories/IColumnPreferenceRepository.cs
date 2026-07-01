using DataWarehousePower.Models;

namespace DataWarehousePower.Repositories
{
    public interface IColumnPreferenceRepository
    {
        Task<UserColumnPreference?> GetAsync(string userId, int reportDefinitionId, string? schemaTemplate);
        Task<UserColumnPreference?> GetDataFileAsync(string userId, int dataFileDefinitionId, string? schemaTemplate);
        Task<UserColumnPreference?> GetByIdAsync(string userId, int reportDefinitionId, int preferenceId);
        Task<List<string>> GetSchemaTemplatesAsync(string userId, int reportDefinitionId);
        Task<List<string>> GetDataFileSchemaTemplatesAsync(string userId, int dataFileDefinitionId);
        Task<Dictionary<string, int>> GetSchemaTemplatePreferenceIdsAsync(string userId, int reportDefinitionId);
        Task<Dictionary<string, int>> GetDataFileSchemaTemplatePreferenceIdsAsync(string userId, int dataFileDefinitionId);
        Task<int> UpsertAsync(UserColumnPreference preference, int? preferenceId = null);
        Task UpdateSchemaTemplateAsync(string userId, int reportDefinitionId, int preferenceId, string newSchemaTemplate);
        Task DeleteAsync(string userId, int reportDefinitionId, int preferenceId);
    }
}
