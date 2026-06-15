using DataWarehousePower.Data;
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
        DateTime cutoffUtc = DateTime.UtcNow.AddDays(-retentionDays);

        int deletedCount = await dbContext.AuditLogs
            .Where(log => log.TimestampUtc < cutoffUtc)
            .ExecuteDeleteAsync();

        logger.LogInformation(
            "Hangfire audit log cleanup deleted {DeletedCount} rows older than {CutoffUtc} with retention {RetentionDays} days.",
            deletedCount,
            cutoffUtc,
            retentionDays);
    }
}
