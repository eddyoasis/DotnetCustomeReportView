namespace DataWarehousePower.Services
{
    public interface IAuditLogService
    {
        Task LogRequestAsync(
            string actionType,
            string userId,
            string username,
            string correlationId,
            string entityName,
            string? entityId,
            string? entityLabel,
            object? metadata,
            string? detail,
            long durationMs,
            int? statusCode,
            CancellationToken cancellationToken = default);

        Task LogActionAsync(
            string actionType,
            string userId,
            string username,
            string correlationId,
            string entityName,
            string? entityId,
            string? entityLabel,
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