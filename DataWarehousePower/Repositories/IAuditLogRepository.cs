using DataWarehousePower.Models;

namespace DataWarehousePower.Repositories
{
    public interface IAuditLogRepository
    {
        Task<AuditLog?> GetByIdAsync(long id);
        Task<(List<AuditLog> Logs, int TotalCount)> SearchAsync(AuditLogFilterViewModel filter);
        Task<List<string>> GetDistinctUsersAsync();
        Task<List<string>> GetDistinctActionTypesAsync();
        Task<List<string>> GetDistinctEntityNamesAsync();
    }
}
