using DataWarehousePower.Models;

namespace DataWarehousePower.Repositories
{
    public interface IReportConnectionStringRepository
    {
        Task<List<ReportConnectionString>> GetAllAsync();
        Task<ReportConnectionString?> GetByIdAsync(int id);
        Task<ReportConnectionString> CreateAsync(ReportConnectionString reportConnectionString);
        Task UpdateAsync(ReportConnectionString reportConnectionString);
        Task DeleteAsync(int id);
    }
}
