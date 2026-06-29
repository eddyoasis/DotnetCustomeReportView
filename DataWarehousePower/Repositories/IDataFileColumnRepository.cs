using DataWarehousePower.Models;

namespace DataWarehousePower.Repositories
{
    public interface IDataFileColumnRepository
    {
        Task<List<DataFileColumn>> GetAllAsync();
        Task<List<DataFileColumn>> GetByDataFileDefinitionIdAsync(int dataFileDefinitionId);
        Task<DataFileColumn?> GetByIdAsync(int id);
        Task<bool> DataFileDefinitionExistsAsync(int dataFileDefinitionId);
        Task<DataFileColumn> CreateAsync(DataFileColumn dataFileColumn);
        Task UpdateAsync(DataFileColumn dataFileColumn);
        Task DeleteAsync(int id);
    }
}