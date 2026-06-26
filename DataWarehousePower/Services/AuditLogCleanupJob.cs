using DataWarehousePower.Data;
using DataWarehousePower.Helper;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DataWarehousePower.Services;

public sealed class AuditLogCleanupJob(
    AppDbContext dbContext,
    IOptions<HangfireOptions> options,
    ILogger<AuditLogCleanupJob> logger) : IAuditLogCleanupJob
{
    [AutomaticRetry(Attempts = 3)]
    public async Task DeleteExpiredLogsAsync()
    {
        HangfireOptions settings = options.Value;
        int retentionDays = Math.Max(1, settings.AuditLogRetentionDays);
        DateTime cutoffLocal = DateTimeHelper.GetCurrentLocalTime().AddDays(-retentionDays);

        int deletedCount = await dbContext.AuditLogs
            .Where(log => log.CreatedAt < cutoffLocal)
            .ExecuteDeleteAsync();

        logger.LogInformation(
            "Hangfire audit log cleanup deleted {DeletedCount} rows older than {CutoffLocal} with retention {RetentionDays} days.",
            deletedCount,
            cutoffLocal,
            retentionDays);
    }
}
