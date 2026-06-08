using DataWarehousePower.Models;

namespace DataWarehousePower.Repositories
{
    public interface IReportStaffRepository
    {
        Task<IEnumerable<ReportStaff>> GetAllAsync();
        Task<ReportStaff?> GetByIdAsync(int id);
    }
}
