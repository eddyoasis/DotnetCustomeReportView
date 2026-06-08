using DataWarehousePower.Models;

namespace DataWarehousePower.Services
{
    public interface IReportStaffService
    {
        Task<IEnumerable<ReportStaff>> GetAllStaffAsync();
        IReadOnlyList<ColumnDefinition> GetAvailableColumns();
    }
}
