using DataWarehousePower.Models;
using DataWarehousePower.Repositories;

namespace DataWarehousePower.Services
{
    public class DepartmentConnectionService(
        IDepartmentConnectionRepository repository,
        IDepartmentRepository departmentRepository
        ) : IDepartmentConnectionService
    {
        private readonly IDepartmentConnectionRepository _repository = repository;
        private readonly IDepartmentRepository _departmentRepository = departmentRepository;

        public async Task<string> GetConnectionStringByUserDepartmentAsync(string userDepartment)
        {
            var departmentId = await _departmentRepository.GetByUserDepartmentAsync(userDepartment);
            var departmentConnection = await _repository.GetByDepartmentIdAsync(departmentId);
            return departmentConnection?.ReportConnectionString?.ConnectionString ?? string.Empty;
        }

        public Task<List<DepartmentConnection>> GetAllAsync()
            => _repository.GetAllAsync();

        public Task<DepartmentConnection?> GetByIdAsync(int id)
            => _repository.GetByIdAsync(id);

        public Task<DepartmentConnection> CreateAsync(DepartmentConnection departmentConnection)
            => _repository.CreateAsync(departmentConnection);

        public Task UpdateAsync(DepartmentConnection departmentConnection)
            => _repository.UpdateAsync(departmentConnection);

        public Task DeleteAsync(int id)
            => _repository.DeleteAsync(id);
    }
}
