using DataWarehousePower.Models;

namespace DataWarehousePower.Services
{
    public interface IReportManageService
    {
        Task<ReportManageListViewModel> GetListViewModelAsync();
        Task<ReportManageFormViewModel> GetFormViewModelAsync(int id);
        Task<ReportDefinition> CreateReportAsync(ReportManageFormViewModel form);
        Task UpdateReportAsync(ReportManageFormViewModel form);
        Task DeleteReportAsync(int id);
        Task ToggleActiveAsync(int id);
    }
}
