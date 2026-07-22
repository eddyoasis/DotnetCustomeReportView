using DataWarehousePower.Models;

namespace DataWarehousePower.Repositories
{
    public interface IDepartmentSnowflakeConnectionRepository
    {
        Task<List<DepartmentSnowflakeConnection>> GetAllAsync();
        Task<DepartmentSnowflakeConnection?> GetByIdAsync(int id);
        Task<DepartmentSnowflakeConnection?> GetByDepartmentIdAsync(int? departmentId);
        Task<DepartmentSnowflakeConnection> CreateAsync(DepartmentSnowflakeConnection departmentSnowflakeConnection);
        Task UpdateAsync(DepartmentSnowflakeConnection departmentSnowflakeConnection);
        Task DeleteAsync(int id);
    }
}