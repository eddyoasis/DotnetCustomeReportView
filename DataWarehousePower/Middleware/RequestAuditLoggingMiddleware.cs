using DataWarehousePower.Data;
using DataWarehousePower.Models.AppSettings;
using DataWarehousePower.Services;
using log4net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;

namespace DataWarehousePower.Middleware;

public sealed class RequestAuditLoggingMiddleware(RequestDelegate next)
{
    private static readonly ILog AuditLogger = LogManager.GetLogger("AuditLogger");
    private static readonly ILog ErrorLogger = LogManager.GetLogger(typeof(RequestAuditLoggingMiddleware));

    public async Task InvokeAsync(
        HttpContext context,
        IAuditLogService auditLogService,
        AppDbContext dbContext,
        IOptions<RequestAuditLoggingOptions> requestAuditLoggingOptions)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        Exception? requestException = null;

        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            requestException = ex;
            throw;
        }
        finally
        {
            stopwatch.Stop();

            try
            {
                await LogRequestAsync(
                    context,
                    auditLogService,
                    dbContext,
                    requestAuditLoggingOptions.Value,
                    stopwatch.ElapsedMilliseconds,
                    requestException);
            }
            catch (Exception logEx)
            {
                ErrorLogger.Error($"Failed to write request audit log. TraceId: {context.TraceIdentifier}", logEx);
            }
        }
    }

    private static async Task LogRequestAsync(
        HttpContext context,
        IAuditLogService auditLogService,
        AppDbContext dbContext,
        RequestAuditLoggingOptions options,
        long durationMs,
        Exception? requestException)
    {
        string path = context.Request.Path.Value ?? string.Empty;
        string method = context.Request.Method;
        int statusCode = requestException is null ? context.Response.StatusCode : StatusCodes.Status500InternalServerError;

        string userId = ResolveUserId(context);
        string username = ResolveUsername(context);

        bool isReportView = IsReportViewRequest(context, method);
        int? reportId = TryResolveReportId(context);
        string? reportName = isReportView && reportId.HasValue
            ? await ResolveReportNameAsync(dbContext, reportId.Value)
            : null;

        Dictionary<string, string?> queryData = context.Request.Query
            .ToDictionary(pair => pair.Key, pair => pair.Value.FirstOrDefault(), StringComparer.OrdinalIgnoreCase);

        Dictionary<string, string?> routeData = context.Request.RouteValues
            .ToDictionary(pair => pair.Key, pair => pair.Value?.ToString(), StringComparer.OrdinalIgnoreCase);

        string actionType = isReportView ? "ReportViewed" : "HttpRequest";
        string entityName = isReportView ? "ReportView" : "HttpRequest";
        string? entityId = isReportView ? reportId?.ToString() : null;
        string? entityLabel = isReportView
            ? (string.IsNullOrWhiteSpace(reportName) ? $"Report #{reportId}" : reportName)
            : null;

        if (ShouldSkipLogging(options, actionType, entityName, path))
        {
            return;
        }

        if (ShouldSkipInitialAuthChallengeLog(context, statusCode, requestException))
        {
            return;
        }

        object metadata = new
        {
            Request = new
            {
                Method = method,
                Path = path,
                Query = queryData,
                RouteValues = routeData,
                Endpoint = context.GetEndpoint()?.DisplayName
            },
            Report = isReportView
                ? new
                {
                    Id = reportId,
                    Name = reportName,
                    Filters = queryData
                }
                : null,
            Response = new
            {
                StatusCode = statusCode,
                DurationMs = durationMs,
                HasException = requestException is not null
            }
        };

        string detail = isReportView
            ? BuildReportDetail(reportId, reportName, queryData, statusCode, durationMs)
            : $"{method} {path}{context.Request.QueryString} returned {statusCode} in {durationMs} ms.";

        string summary = isReportView
            ? BuildReportSummary(method, path, statusCode, durationMs, reportId, reportName, queryData, context.TraceIdentifier)
            : $"HTTP {method} {path}{context.Request.QueryString} => {statusCode} ({durationMs} ms) User:{userId} TraceId:{context.TraceIdentifier}";

        AuditLogger.Info(summary);

        await auditLogService.LogRequestAsync(
            actionType: actionType,
            userId: userId,
            username: username,
            correlationId: context.TraceIdentifier,
            entityName: entityName,
            entityId: entityId,
            entityLabel: entityLabel,
            metadata: metadata,
            detail: detail,
            durationMs: durationMs,
            statusCode: statusCode,
            cancellationToken: CancellationToken.None);
    }

    private static bool ShouldSkipLogging(
        RequestAuditLoggingOptions options,
        string actionType,
        string entityName,
        string path)
    {
        if (ContainsIgnoreCase(options.ExcludedEntityNames, entityName))
        {
            return true;
        }

        if (ContainsIgnoreCase(options.ExcludedActionTypes, actionType))
        {
            return true;
        }

        if (options.ExcludedPathPrefixes.Any(prefix =>
                !string.IsNullOrWhiteSpace(prefix)
                && path.StartsWith(prefix.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    private static bool ContainsIgnoreCase(IEnumerable<string> values, string expected)
        => values.Any(value => string.Equals(value?.Trim(), expected, StringComparison.OrdinalIgnoreCase));

    private static bool ShouldSkipInitialAuthChallengeLog(
        HttpContext context,
        int statusCode,
        Exception? requestException)
    {
        if (requestException is not null || statusCode != StatusCodes.Status401Unauthorized)
        {
            return false;
        }

        if (context.User?.Identity?.IsAuthenticated == true)
        {
            return false;
        }

        return !context.Request.Headers.ContainsKey("Authorization");
    }

    private static string BuildReportDetail(
        int? reportId,
        string? reportName,
        Dictionary<string, string?> queryData,
        int statusCode,
        long durationMs)
    {
        string reportLabel = string.IsNullOrWhiteSpace(reportName)
            ? (reportId.HasValue ? $"Report #{reportId.Value}" : "Report")
            : reportName;

        string filters = SerializeCompact(queryData, 1000);
        return $"Viewed {reportLabel}. Filters={filters}. Status={statusCode}. DurationMs={durationMs}.";
    }

    private static string BuildReportSummary(
        string method,
        string path,
        int statusCode,
        long durationMs,
        int? reportId,
        string? reportName,
        Dictionary<string, string?> queryData,
        string traceId)
    {
        string reportLabel = string.IsNullOrWhiteSpace(reportName)
            ? (reportId.HasValue ? $"Report #{reportId.Value}" : "Report")
            : reportName;

        string filters = SerializeCompact(queryData, 1200);
        return $"REPORT {method} {path} => {statusCode} ({durationMs} ms) Report:{reportLabel} Filters:{filters} TraceId:{traceId}";
    }

    private static string ResolveUserId(HttpContext context)
    {
        string? claimUserId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(claimUserId))
        {
            return claimUserId.Trim();
        }

        string? identityName = context.User.Identity?.Name;
        if (!string.IsNullOrWhiteSpace(identityName))
        {
            return identityName.Trim();
        }

        return "Anonymous";
    }

    private static string ResolveUsername(HttpContext context)
    {
        string? displayName = context.Session.GetString("UserDisplayName");
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName.Trim();
        }

        return ResolveUserId(context);
    }

    private static bool IsReportViewRequest(HttpContext context, string method)
    {
        if (!HttpMethods.IsGet(method))
        {
            return false;
        }

        string? controller = context.Request.RouteValues.TryGetValue("controller", out object? controllerValue)
            ? controllerValue?.ToString()
            : null;

        string? action = context.Request.RouteValues.TryGetValue("action", out object? actionValue)
            ? actionValue?.ToString()
            : null;

        return string.Equals(controller, "Report", StringComparison.OrdinalIgnoreCase)
            && string.Equals(action, "Index", StringComparison.OrdinalIgnoreCase);
    }

    private static int? TryResolveReportId(HttpContext context)
    {
        if (context.Request.RouteValues.TryGetValue("id", out object? routeIdValue)
            && int.TryParse(routeIdValue?.ToString(), out int routeId))
        {
            return routeId;
        }

        if (context.Request.Query.TryGetValue("id", out var queryIdValues)
            && int.TryParse(queryIdValues.FirstOrDefault(), out int queryId))
        {
            return queryId;
        }

        return null;
    }

    private static async Task<string?> ResolveReportNameAsync(AppDbContext dbContext, int reportId)
        => await dbContext.ReportDefinitions
            .AsNoTracking()
            .Where(report => report.Id == reportId)
            .Select(report => report.ReportName)
            .FirstOrDefaultAsync();

    private static string SerializeCompact(object data, int maxLength)
    {
        string serialized = JsonSerializer.Serialize(data);
        if (serialized.Length <= maxLength)
        {
            return serialized;
        }

        return serialized[..maxLength] + "...";
    }
}
