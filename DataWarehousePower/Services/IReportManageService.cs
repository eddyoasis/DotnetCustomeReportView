using DataWarehousePower.Models;

namespace DataWarehousePower.Services
{
    public interface IReportManageService
    {
        Task<List<string>> GetSourceDatabaseOptionsAsync();
        Task<List<string>> GetSourceTableOptionsAsync(string? sourceDatabase);
        Task<List<string>> GetSourceStoredProcedureOptionsAsync(string? sourceDatabase);
        Task<List<string>> GetSourceColumnsAsync(string? sourceDatabase, string? sourceTable, string? sourceSP);
        Task<List<string>> GetSourceParametersAsync(string? sourceDatabase, string? sourceTable, string? sourceSP);
        Task<ReportManageListViewModel> GetListViewModelAsync();
        Task<ReportManageFormViewModel> GetFormViewModelAsync(int id);
        Task<ReportDefinition> CreateReportAsync(ReportManageFormViewModel form);
        Task UpdateReportAsync(ReportManageFormViewModel form);
        Task DeleteReportAsync(int id);
        Task ToggleActiveAsync(int id);
    }
}
