using DataWarehousePower.Data;
using DataWarehousePower.Models;
using System.Text.Json;

namespace DataWarehousePower.Services
{
    public class AuditLogService : IAuditLogService
    {
        private readonly AppDbContext _context;

        public AuditLogService(AppDbContext context)
        {
            _context = context;
        }

        public async Task LogActionAsync(
            string actionType,
            string userId,
            string username,
            string correlationId,
            string entityName,
            string? entityId,
            object? oldValues,
            object? newValues,
            string? detail,
            CancellationToken cancellationToken = default)
        {
            object metadata = new
            {
                Detail = detail
            };

            string description = BuildDescription(username, actionType, entityName, entityId, detail);

            AuditLog auditLog = new()
            {
                UserId = userId,
                Username = username,
                ActionType = actionType,
                Description = description,
                EntityName = entityName,
                EntityId = entityId,
                OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
                NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues),
                Metadata = JsonSerializer.Serialize(metadata),
                CorrelationId = correlationId,
                TimestampUtc = DateTime.UtcNow
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task LogExportAsync(
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
            CancellationToken cancellationToken = default)
        {
            object metadata = new
            {
                ReportId = reportId,
                ReportName = reportName,
                Format = string.IsNullOrWhiteSpace(format) ? "unknown" : format.Trim().ToLowerInvariant(),
                ClientCode = clientCode,
                FilterClientCode = filterClientCode,
                DateFromUtc = dateFrom,
                DateToUtc = dateTo,
                Detail = detail
            };

            string description = BuildDescription(username, actionType, "ReportExport", reportName ?? $"Report #{reportId}", detail);

            AuditLog auditLog = new()
            {
                UserId = userId,
                Username = username,
                ActionType = actionType,
                Description = description,
                EntityName = "ReportExport",
                EntityId = reportId.ToString(),
                Metadata = JsonSerializer.Serialize(metadata),
                CorrelationId = correlationId,
                TimestampUtc = DateTime.UtcNow
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync(cancellationToken);
        }

        private static string BuildDescription(string username, string actionType, string entityName, string? entityLabel, string? detail)
        {
            string actionLabel = actionType switch
            {
                var value when value.Contains("Create", StringComparison.OrdinalIgnoreCase) => "created",
                var value when value.Contains("Update", StringComparison.OrdinalIgnoreCase) => "updated",
                var value when value.Contains("Delete", StringComparison.OrdinalIgnoreCase) => "deleted",
                var value when value.Contains("Export", StringComparison.OrdinalIgnoreCase) => "performed export",
                _ => "performed"
            };

            string targetLabel = string.IsNullOrWhiteSpace(entityLabel) ? entityName : entityLabel;
            string suffix = string.IsNullOrWhiteSpace(detail) ? string.Empty : $" Detail: {detail}";
            return $"{username} {actionLabel} {entityName} ({targetLabel}).{suffix}";
        }
    }
}