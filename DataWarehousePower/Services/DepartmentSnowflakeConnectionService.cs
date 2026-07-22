using DataWarehousePower.Models;
using DataWarehousePower.Repositories;

namespace DataWarehousePower.Services
{
    public class DepartmentSnowflakeConnectionService : IDepartmentSnowflakeConnectionService
    {
        private readonly IDepartmentSnowflakeConnectionRepository _repository;

        public DepartmentSnowflakeConnectionService(IDepartmentSnowflakeConnectionRepository repository)
        {
            _repository = repository;
        }

        public Task<List<DepartmentSnowflakeConnection>> GetAllAsync()
            => _repository.GetAllAsync();

        public Task<DepartmentSnowflakeConnection?> GetByIdAsync(int id)
            => _repository.GetByIdAsync(id);

        public Task<DepartmentSnowflakeConnection> CreateAsync(DepartmentSnowflakeConnection departmentSnowflakeConnection)
            => _repository.CreateAsync(departmentSnowflakeConnection);

        public Task UpdateAsync(DepartmentSnowflakeConnection departmentSnowflakeConnection)
            => _repository.UpdateAsync(departmentSnowflakeConnection);

        public Task DeleteAsync(int id)
            => _repository.DeleteAsync(id);
    }
}