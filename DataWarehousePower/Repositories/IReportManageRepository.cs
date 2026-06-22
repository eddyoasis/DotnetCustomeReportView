using DataWarehousePower.Models;

namespace DataWarehousePower.Repositories
{
    public interface IReportManageRepository
    {
        Task<List<string>> GetSourceDatabaseOptionsAsync();
        Task<List<ReportDefinition>> GetAllWithColumnsAsync();
        Task<ReportDefinition?> GetByIdWithColumnsAsync(int id);
        Task<ReportDefinition> CreateAsync(ReportDefinition report, IEnumerable<ReportColumn> columns);
        Task UpdateAsync(ReportDefinition report, IEnumerable<ReportColumn> columns, IEnumerable<int> deletedColumnIds);
        Task DeleteAsync(int id);
        Task ToggleActiveAsync(int id);
    }
}
