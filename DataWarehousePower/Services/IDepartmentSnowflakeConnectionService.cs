using DataWarehousePower.Models;

namespace DataWarehousePower.Services
{
    public interface IDepartmentSnowflakeConnectionService
    {
        Task<string> GetConnectionStringByUserDepartmentAsync(string userDepartment);
        Task<List<DepartmentSnowflakeConnection>> GetAllAsync();
        Task<DepartmentSnowflakeConnection?> GetByIdAsync(int id);
        Task<DepartmentSnowflakeConnection> CreateAsync(DepartmentSnowflakeConnection departmentSnowflakeConnection);
        Task UpdateAsync(DepartmentSnowflakeConnection departmentSnowflakeConnection);
        Task DeleteAsync(int id);
    }
}