using DataWarehousePower.Data;
using DataWarehousePower.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System.Text.Json;

namespace DataWarehousePower.Services
{
    public class AuditLogService : IAuditLogService
    {
        private readonly AppDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditLogService(AppDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task LogRequestAsync(
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
            CancellationToken cancellationToken = default)
        {
            object metadataPayload = new
            {
                Detail = detail,
                Data = metadata
            };

            string description = BuildDescription(username, actionType, entityName, entityId, entityLabel, detail);
            RequestAuditContext requestContext = ResolveRequestContext();

            AuditLog auditLog = new()
            {
                UserId = userId,
                Username = username,
                ActionType = actionType,
                Description = description,
                EntityName = entityName,
                EntityId = entityId,
                Metadata = JsonSerializer.Serialize(metadataPayload),
                CorrelationId = correlationId,
                IpAddress = requestContext.IpAddress,
                Host = requestContext.Host,
                RequestMethod = requestContext.RequestMethod,
                RequestPath = requestContext.RequestPath,
                QueryString = requestContext.QueryString,
                UserAgent = requestContext.UserAgent,
                Referrer = requestContext.Referrer,
                Protocol = requestContext.Protocol,
                StatusCode = statusCode ?? requestContext.StatusCode,
                SessionId = requestContext.SessionId,
                DurationMs = durationMs,
                TimestampUtc = DateTime.UtcNow
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task LogActionAsync(
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
            CancellationToken cancellationToken = default)
        {
            object metadata = new
            {
                Detail = detail
            };

            string description = BuildDescription(username, actionType, entityName, entityId, entityLabel, detail);
            RequestAuditContext requestContext = ResolveRequestContext();

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
                IpAddress = requestContext.IpAddress,
                Host = requestContext.Host,
                RequestMethod = requestContext.RequestMethod,
                RequestPath = requestContext.RequestPath,
                QueryString = requestContext.QueryString,
                UserAgent = requestContext.UserAgent,
                Referrer = requestContext.Referrer,
                Protocol = requestContext.Protocol,
                StatusCode = requestContext.StatusCode,
                SessionId = requestContext.SessionId,
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

            string reportLabel = BuildExportEntityLabel(reportName, reportId, clientCode, filterClientCode);
            string description = BuildDescription(username, actionType, "ReportExport", reportId.ToString(), reportLabel, detail);
            RequestAuditContext requestContext = ResolveRequestContext();

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
                IpAddress = requestContext.IpAddress,
                Host = requestContext.Host,
                RequestMethod = requestContext.RequestMethod,
                RequestPath = requestContext.RequestPath,
                QueryString = requestContext.QueryString,
                UserAgent = requestContext.UserAgent,
                Referrer = requestContext.Referrer,
                Protocol = requestContext.Protocol,
                StatusCode = requestContext.StatusCode,
                SessionId = requestContext.SessionId,
                TimestampUtc = DateTime.UtcNow
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync(cancellationToken);
        }

        private static string BuildExportEntityLabel(string? reportName, int reportId, string? clientCode, string? filterClientCode)
        {
            string normalizedReportName = string.IsNullOrWhiteSpace(reportName)
                ? $"Report #{reportId}"
                : reportName.Trim();

            string? normalizedClientCode = string.IsNullOrWhiteSpace(clientCode)
                ? "Default"
                : clientCode.Trim();

            string? normalizedFilterClientCode = string.IsNullOrWhiteSpace(filterClientCode)
                ? "All"
                : filterClientCode.Trim();

            return $"{normalizedReportName}({normalizedClientCode}) - {normalizedFilterClientCode}";
        }

        //private static string BuildExportEntityLabel(string? reportName, int reportId, string? clientCode, string? filterClientCode)
        //{
        //    string normalizedReportName = string.IsNullOrWhiteSpace(reportName)
        //        ? $"Report #{reportId}"
        //        : reportName.Trim();

        //    string? normalizedClientCode = string.IsNullOrWhiteSpace(clientCode)
        //        ? null
        //        : clientCode.Trim();

        //    if (string.IsNullOrWhiteSpace(normalizedClientCode))
        //    {
        //        normalizedClientCode = string.IsNullOrWhiteSpace(filterClientCode)
        //            ? null
        //            : filterClientCode.Trim();
        //    }

        //    return string.IsNullOrWhiteSpace(normalizedClientCode)
        //        ? normalizedReportName
        //        : $"{normalizedReportName} - {normalizedClientCode}";
        //}

        private static string BuildDescription(string username, string actionType, string entityName, string? entityId, string? entityLabel, string? detail)
        {
            string actionLabel = actionType switch
            {
                var value when value.Contains("Create", StringComparison.OrdinalIgnoreCase) => "created",
                var value when value.Contains("Update", StringComparison.OrdinalIgnoreCase) => "updated",
                var value when value.Contains("Delete", StringComparison.OrdinalIgnoreCase) => "deleted",
                var value when value.Contains("Export", StringComparison.OrdinalIgnoreCase) => "performed export",
                _ => "performed"
            };

            string targetLabel = string.IsNullOrWhiteSpace(entityLabel)
                ? string.IsNullOrWhiteSpace(entityId) ? entityName : entityId
                : entityLabel;
            string suffix = string.IsNullOrWhiteSpace(detail) ? string.Empty : $" Detail: {detail}";
            return $"{username} {actionLabel} {entityName} ({targetLabel}).{suffix}";
        }

        private RequestAuditContext ResolveRequestContext()
        {
            HttpContext? context = _httpContextAccessor.HttpContext;

            return new RequestAuditContext
            {
                IpAddress = ResolveIpAddress(context),
                Host = ResolveHost(context),
                RequestMethod = ResolveRequestMethod(context),
                RequestPath = ResolveRequestPath(context),
                QueryString = ResolveQueryString(context),
                UserAgent = ResolveHeaderValue(context, "User-Agent", 1024),
                Referrer = ResolveHeaderValue(context, "Referer", 1024),
                Protocol = ResolveProtocol(context),
                StatusCode = context?.Response?.StatusCode,
                SessionId = ResolveSessionId(context)
            };
        }

        private static string? ResolveIpAddress(HttpContext? context)
        {
            if (context is null)
            {
                return null;
            }

            string? forwarded = ResolveHeaderValue(context, "X-Forwarded-For", 256);
            if (!string.IsNullOrWhiteSpace(forwarded))
            {
                string firstIp = forwarded.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(firstIp))
                {
                    return firstIp.Length > 64 ? firstIp[..64] : firstIp;
                }
            }

            string? realIp = ResolveHeaderValue(context, "X-Real-IP", 64);
            if (!string.IsNullOrWhiteSpace(realIp))
            {
                return realIp;
            }

            string? remoteIp = context.Connection.RemoteIpAddress?.ToString();
            if (string.IsNullOrWhiteSpace(remoteIp))
            {
                return null;
            }

            return remoteIp.Length > 64 ? remoteIp[..64] : remoteIp;
        }

        private static string? ResolveHost(HttpContext? context)
        {
            string? host = context?.Request.Host.Value;
            if (string.IsNullOrWhiteSpace(host))
            {
                return null;
            }

            string trimmed = host.Trim();
            return trimmed.Length > 256 ? trimmed[..256] : trimmed;
        }

        private static string? ResolveRequestMethod(HttpContext? context)
        {
            string? method = context?.Request.Method;
            if (string.IsNullOrWhiteSpace(method))
            {
                return null;
            }

            string trimmed = method.Trim();
            return trimmed.Length > 16 ? trimmed[..16] : trimmed;
        }

        private static string? ResolveRequestPath(HttpContext? context)
        {
            if (context is null)
            {
                return null;
            }

            string pathValue = $"{context.Request.PathBase}{context.Request.Path}";
            if (string.IsNullOrWhiteSpace(pathValue))
            {
                return null;
            }

            return pathValue.Length > 2048 ? pathValue[..2048] : pathValue;
        }

        private static string? ResolveQueryString(HttpContext? context)
        {
            string? queryValue = context?.Request.QueryString.Value;
            if (string.IsNullOrWhiteSpace(queryValue))
            {
                return null;
            }

            return queryValue.Length > 2048 ? queryValue[..2048] : queryValue;
        }

        private static string? ResolveProtocol(HttpContext? context)
        {
            string? protocol = context?.Request.Protocol;
            if (string.IsNullOrWhiteSpace(protocol))
            {
                return null;
            }

            string trimmed = protocol.Trim();
            return trimmed.Length > 16 ? trimmed[..16] : trimmed;
        }

        private static string? ResolveSessionId(HttpContext? context)
        {
            if (context is null)
            {
                return null;
            }

            try
            {
                string? sessionId = context.Session.Id;
                if (string.IsNullOrWhiteSpace(sessionId))
                {
                    return null;
                }

                string trimmed = sessionId.Trim();
                return trimmed.Length > 128 ? trimmed[..128] : trimmed;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        private static string? ResolveHeaderValue(HttpContext? context, string headerName, int maxLength)
        {
            if (context is null)
            {
                return null;
            }

            if (!context.Request.Headers.TryGetValue(headerName, out StringValues values))
            {
                return null;
            }

            string? value = values.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string trimmed = value.Trim();
            return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
        }

        private sealed class RequestAuditContext
        {
            public string? IpAddress { get; init; }
            public string? Host { get; init; }
            public string? RequestMethod { get; init; }
            public string? RequestPath { get; init; }
            public string? QueryString { get; init; }
            public string? UserAgent { get; init; }
            public string? Referrer { get; init; }
            public string? Protocol { get; init; }
            public int? StatusCode { get; init; }
            public string? SessionId { get; init; }
        }
    }
}