using DataWarehousePower.Models;

namespace DataWarehousePower.Repositories
{
    public interface IDashboardRepository
    {
        Task<List<DashboardScheduledJobItem>> GetUserScheduledJobsAsync(string userId);
        Task<List<DashboardReportItem>> GetUserReportsAsync(string? userDepartment);
        Task<List<DashboardDataFileItem>> GetUserDataFilesAsync(string userId);
        Task<List<DashboardDataFileItem>> GetUserDataFilesAsync(string userId, string? userDepartment);
    }
}
