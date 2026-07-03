using DataWarehousePower.Models;

namespace DataWarehousePower.Services
{
    public interface IDepartmentConnectionService
    {
        Task<List<DepartmentConnection>> GetAllAsync();
        Task<DepartmentConnection?> GetByIdAsync(int id);
        Task<DepartmentConnection> CreateAsync(DepartmentConnection departmentConnection);
        Task UpdateAsync(DepartmentConnection departmentConnection);
        Task DeleteAsync(int id);
    }
}
