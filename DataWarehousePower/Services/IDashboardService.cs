using DataWarehousePower.Models;

namespace DataWarehousePower.Services
{
    public interface IDashboardService
    {
        Task<DashboardViewModel> BuildDashboardViewModelAsync(string userId, string? userDepartment);
    }
}
