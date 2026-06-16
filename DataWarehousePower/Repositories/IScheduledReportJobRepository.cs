using DataWarehousePower.Models;

namespace DataWarehousePower.Repositories;

public interface IScheduledReportJobRepository
{
    Task<List<ScheduledReportJob>> GetAllAsync();
    Task<List<ScheduledReportJob>> GetAllByUserIdAsync(string userId);
    Task<ScheduledReportJob?> GetByIdAsync(int id);
    Task<ScheduledReportJob?> GetByIdForUpdateAsync(int id);
    Task<ScheduledReportJob?> GetByIdForUserAsync(int id, string userId);
    Task<ScheduledReportJob?> GetByIdForUserUpdateAsync(int id, string userId);
    Task AddAsync(ScheduledReportJob entity);
    Task UpdateAsync(ScheduledReportJob entity);
    Task DeleteAsync(ScheduledReportJob entity);
}
