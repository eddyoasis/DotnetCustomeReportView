using DataWarehousePower.Models;

namespace DataWarehousePower.Repositories;

public interface IScheduledReportJobRepository
{
    Task<List<ScheduledReportJob>> GetAllAsync();
    Task<ScheduledReportJob?> GetByIdAsync(int id);
    Task<ScheduledReportJob?> GetByIdForUpdateAsync(int id);
    Task AddAsync(ScheduledReportJob entity);
    Task UpdateAsync(ScheduledReportJob entity);
    Task DeleteAsync(ScheduledReportJob entity);
}
