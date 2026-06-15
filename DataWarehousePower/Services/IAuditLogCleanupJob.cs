namespace DataWarehousePower.Services;

public interface IAuditLogCleanupJob
{
    Task DeleteExpiredLogsAsync();
}
