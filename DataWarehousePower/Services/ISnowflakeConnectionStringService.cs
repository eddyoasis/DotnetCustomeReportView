using DataWarehousePower.Models;

namespace DataWarehousePower.Services
{
    public interface ISnowflakeConnectionStringService
    {
        Task<List<SnowflakeConnectionString>> GetAllAsync();
        Task<SnowflakeConnectionString?> GetByIdAsync(int id);
        Task<SnowflakeConnectionString> CreateAsync(SnowflakeConnectionString snowflakeConnectionString);
        Task UpdateAsync(SnowflakeConnectionString snowflakeConnectionString);
        Task DeleteAsync(int id);
    }
}