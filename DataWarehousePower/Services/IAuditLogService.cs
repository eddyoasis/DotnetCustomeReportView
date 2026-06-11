namespace DataWarehousePower.Services
{
    public interface IAuditLogService
    {
        Task LogActionAsync(
            string actionType,
            string userId,
            string username,
            string correlationId,
            string entityName,
            string? entityId,
            object? oldValues,
            object? newValues,
            string? detail,
            CancellationToken cancellationToken = default);

        Task LogExportAsync(
            string actionType,
            string userId,
            string username,
            string correlationId,
            int reportId,
            string? reportName,
            string? format,
            string? clientCode,
            string? filterClientCode,
            DateTime? dateFrom,
            DateTime? dateTo,
            string detail,
            CancellationToken cancellationToken = default);
    }
}