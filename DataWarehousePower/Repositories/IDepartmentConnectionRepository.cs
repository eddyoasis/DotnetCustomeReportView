using DataWarehousePower.Models;

namespace DataWarehousePower.Repositories
{
    public interface IDepartmentConnectionRepository
    {
        Task<List<DepartmentConnection>> GetAllAsync();
        Task<DepartmentConnection?> GetByDepartmentIdAsync(int? departmentId);
        Task<DepartmentConnection?> GetByIdAsync(int id);
        Task<DepartmentConnection> CreateAsync(DepartmentConnection departmentConnection);
        Task UpdateAsync(DepartmentConnection departmentConnection);
        Task DeleteAsync(int id);
    }
}
