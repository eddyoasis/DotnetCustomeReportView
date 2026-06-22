using DataWarehousePower.Models;

namespace DataWarehousePower.Services;

public interface IScheduledReportJobService
{
    Task<ScheduledJobListViewModel> GetListViewModelAsync(string userId, ScheduledJobFilterViewModel? filter = null);
    Task<int?> FindExistingJobIdAsync(string userId, int reportDefinitionId, string? schemaTemplate, string? clientCode);
    Task<ScheduledJobFormViewModel> GetCreateFormAsync(string userId, string? userDepartment = null);
    Task<ScheduledJobFormViewModel> GetEditFormAsync(int id, string userId, string? userDepartment = null);
    Task<int> CreateAsync(ScheduledJobFormViewModel form, string userId, string username, string userDepartment);
    Task UpdateAsync(ScheduledJobFormViewModel form, string userId, string username, string userDepartment);
    Task DeleteAsync(int id, string userId);
    Task SyncRecurringJobsAsync();
}
