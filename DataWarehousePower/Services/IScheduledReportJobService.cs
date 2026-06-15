using DataWarehousePower.Models;

namespace DataWarehousePower.Services;

public interface IScheduledReportJobService
{
    Task<ScheduledJobListViewModel> GetListViewModelAsync();
    Task<ScheduledJobFormViewModel> GetCreateFormAsync();
    Task<ScheduledJobFormViewModel> GetEditFormAsync(int id);
    Task<int> CreateAsync(ScheduledJobFormViewModel form, string userId, string username);
    Task UpdateAsync(ScheduledJobFormViewModel form, string userId, string username);
    Task DeleteAsync(int id);
    Task SyncRecurringJobsAsync();
}
