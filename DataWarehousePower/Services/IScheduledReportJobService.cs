using DataWarehousePower.Models;

namespace DataWarehousePower.Services;

public interface IScheduledReportJobService
{
    Task<ScheduledJobListViewModel> GetListViewModelAsync(string userId, ScheduledJobFilterViewModel? filter = null);
    Task<ScheduledJobFormViewModel> GetCreateFormAsync(string userId);
    Task<ScheduledJobFormViewModel> GetEditFormAsync(int id, string userId);
    Task<int> CreateAsync(ScheduledJobFormViewModel form, string userId, string username);
    Task UpdateAsync(ScheduledJobFormViewModel form, string userId, string username);
    Task DeleteAsync(int id, string userId);
    Task SyncRecurringJobsAsync();
}
