using DataWarehousePower.Models;
using DataWarehousePower.Repositories;

namespace DataWarehousePower.Services
{
    public class DepartmentConnectionService : IDepartmentConnectionService
    {
        private readonly IDepartmentConnectionRepository _repository;

        public DepartmentConnectionService(IDepartmentConnectionRepository repository)
        {
            _repository = repository;
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
