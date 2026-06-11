using DataWarehousePower.Models;

namespace DataWarehousePower.Services
{
    public interface IAuditLogQueryService
    {
        Task<AuditLogListViewModel> GetListAsync(AuditLogFilterViewModel filter);
        Task<AuditLogDetailViewModel?> GetDetailAsync(long id);
    }
}
