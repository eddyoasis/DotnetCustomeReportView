using DataWarehousePower.Models;
using DataWarehousePower.Repositories;

namespace DataWarehousePower.Services
{
    public class DepartmentService : IDepartmentService
    {
        private readonly IDepartmentRepository _repository;

        public DepartmentService(IDepartmentRepository repository)
        {
            _repository = repository;
        }

        public Task<List<Department>> GetAllAsync()
            => _repository.GetAllAsync();

        public Task<Department?> GetByIdAsync(int id)
            => _repository.GetByIdAsync(id);

        public async Task<Department> CreateAsync(Department department)
        {
            department.CreatedAt = DateTime.Now;
            return await _repository.CreateAsync(department);
        }

        public async Task UpdateAsync(Department department)
        {
            department.ModifiedAt = DateTime.Now;
            await _repository.UpdateAsync(department);
        }

        public Task DeleteAsync(int id)
            => _repository.DeleteAsync(id);

        public Task ToggleActiveAsync(int id)
            => _repository.ToggleActiveAsync(id);
    }
}
