using DataWarehousePower.Models;

namespace DataWarehousePower.Repositories
{
    public interface IDepartmentRepository
    {
        Task<int?> GetByUserDepartmentAsync(string userDepartment);
        Task<List<Department>> GetAllAsync();
        Task<Department?> GetByIdAsync(int id);
        Task<Department> CreateAsync(Department department);
        Task UpdateAsync(Department department);
        Task DeleteAsync(int id);
        Task ToggleActiveAsync(int id);
    }
}
