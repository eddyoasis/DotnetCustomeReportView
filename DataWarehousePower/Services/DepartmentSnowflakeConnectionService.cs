using DataWarehousePower.Models;
using DataWarehousePower.Repositories;

namespace DataWarehousePower.Services
{
    public class DepartmentSnowflakeConnectionService : IDepartmentSnowflakeConnectionService
    {
        private readonly IDepartmentSnowflakeConnectionRepository _repository;
        private readonly IDepartmentRepository _departmentRepository;

        public DepartmentSnowflakeConnectionService(
            IDepartmentSnowflakeConnectionRepository repository,
            IDepartmentRepository departmentRepository)
        {
            _repository = repository;
            _departmentRepository = departmentRepository;
        }

        public async Task<string> GetConnectionStringByUserDepartmentAsync(string userDepartment)
        {
            var departmentId = await _departmentRepository.GetByUserDepartmentAsync(userDepartment);
            var departmentConnection = await _repository.GetByDepartmentIdAsync(departmentId);
            return departmentConnection?.SnowflakeConnectionString?.ConnectionString ?? string.Empty;
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