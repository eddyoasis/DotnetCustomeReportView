using DataWarehousePower.Models;

namespace DataWarehousePower.Services
{
    public interface IDataFileColumnService
    {
        Task<List<DataFileColumn>> GetAllAsync();
        Task<List<DataFileColumn>> GetByDataFileDefinitionIdAsync(int dataFileDefinitionId);
        Task<DataFileColumn?> GetByIdAsync(int id);
        Task<DataFileColumn> CreateAsync(DataFileColumn dataFileColumn);
        Task UpdateAsync(DataFileColumn dataFileColumn);
        Task DeleteAsync(int id);
    }
}