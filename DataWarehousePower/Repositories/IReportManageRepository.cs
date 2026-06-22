using DataWarehousePower.Models;

namespace DataWarehousePower.Repositories
{
    public interface IReportManageRepository
    {
        Task<List<string>> GetSourceDatabaseOptionsAsync();
        Task<List<string>> GetSourceTableOptionsAsync(string? sourceDatabase);
        Task<List<string>> GetSourceStoredProcedureOptionsAsync(string? sourceDatabase);
        Task<List<string>> GetSourceColumnsAsync(string? sourceDatabase, string? sourceTable, string? sourceSP);
        Task<List<ReportDefinition>> GetAllWithColumnsAsync();
        Task<ReportDefinition?> GetByIdWithColumnsAsync(int id);
        Task<ReportDefinition> CreateAsync(ReportDefinition report, IEnumerable<ReportColumn> columns);
        Task UpdateAsync(ReportDefinition report, IEnumerable<ReportColumn> columns, IEnumerable<int> deletedColumnIds);
        Task DeleteAsync(int id);
        Task ToggleActiveAsync(int id);
    }
}
