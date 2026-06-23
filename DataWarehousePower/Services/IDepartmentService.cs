using DataWarehousePower.Models;

namespace DataWarehousePower.Services
{
    public interface IDepartmentService
    {
        Task<List<Department>> GetAllAsync();
        Task<Department?> GetByIdAsync(int id);
        Task<Department> CreateAsync(Department department);
        Task UpdateAsync(Department department);
        Task DeleteAsync(int id);
        Task ToggleActiveAsync(int id);
    }
}
