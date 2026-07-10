using DataWarehousePower.Models;

namespace DataWarehousePower.Services
{
    public interface IDWSchemeService
    {
        Task<List<DWScheme>> GetAllAsync();
        Task<DWScheme?> GetByIdAsync(int id);
        Task<DWScheme> CreateAsync(DWScheme dwScheme);
        Task UpdateAsync(DWScheme dwScheme);
        Task DeleteAsync(int id);
    }
}
