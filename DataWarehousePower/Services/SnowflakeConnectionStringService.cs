using DataWarehousePower.Models;
using DataWarehousePower.Repositories;

namespace DataWarehousePower.Services
{
    public class SnowflakeConnectionStringService : ISnowflakeConnectionStringService
    {
        private readonly ISnowflakeConnectionStringRepository _repository;

        public SnowflakeConnectionStringService(ISnowflakeConnectionStringRepository repository)
        {
            _repository = repository;
        }

        public Task<List<SnowflakeConnectionString>> GetAllAsync()
            => _repository.GetAllAsync();

        public Task<SnowflakeConnectionString?> GetByIdAsync(int id)
            => _repository.GetByIdAsync(id);

        public Task<SnowflakeConnectionString> CreateAsync(SnowflakeConnectionString snowflakeConnectionString)
        {
            Normalize(snowflakeConnectionString);
            return _repository.CreateAsync(snowflakeConnectionString);
        }

        public Task UpdateAsync(SnowflakeConnectionString snowflakeConnectionString)
        {
            Normalize(snowflakeConnectionString);
            return _repository.UpdateAsync(snowflakeConnectionString);
        }

        public Task DeleteAsync(int id)
            => _repository.DeleteAsync(id);

        private static void Normalize(SnowflakeConnectionString snowflakeConnectionString)
        {
            snowflakeConnectionString.Name = snowflakeConnectionString.Name.Trim();
            snowflakeConnectionString.Description = string.IsNullOrWhiteSpace(snowflakeConnectionString.Description)
                ? null
                : snowflakeConnectionString.Description.Trim();
            snowflakeConnectionString.ConnectionString = snowflakeConnectionString.ConnectionString.Trim();
        }
    }
}