using DataWarehousePower.Models;

namespace DataWarehousePower.Repositories
{
    public interface ISnowflakeConnectionStringRepository
    {
        Task<List<SnowflakeConnectionString>> GetAllAsync();
        Task<SnowflakeConnectionString?> GetByIdAsync(int id);
        Task<SnowflakeConnectionString> CreateAsync(SnowflakeConnectionString snowflakeConnectionString);
        Task UpdateAsync(SnowflakeConnectionString snowflakeConnectionString);
        Task DeleteAsync(int id);
    }
}