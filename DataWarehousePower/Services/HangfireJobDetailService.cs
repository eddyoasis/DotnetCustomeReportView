using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using DataWarehousePower.Data;
using DataWarehousePower.Models;
using DataWarehousePower.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DataWarehousePower.Services;

public sealed class HangfireJobDetailService(
    IScheduledReportJobRepository scheduledJobRepository,
    AppDbContext dbContext,
    IConfiguration configuration,
    ILogger<HangfireJobDetailService> logger) : IHangfireJobDetailService
{
    private const string DefaultHangfireSchemaName = "ReportViewer_Hangfire";
    private const string RecurringJobIdParameterName = "RecurringJobId";
    private const string SucceededStateName = "Succeeded";
    private const string FailedStateName = "Failed";
    private static readonly Regex ValidSchemaNamePattern = new("^[A-Za-z0-9_]+$", RegexOptions.Compiled);

    public async Task<HangfireJobDetailViewModel?> GetJobDetailAsync(int scheduledJobId, string userId)
    {
        ScheduledReportJob? scheduledJob = await scheduledJobRepository.GetByIdForUserAsync(scheduledJobId, userId);
        if (scheduledJob is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(scheduledJob.HangfireJobId))
        {
            return new HangfireJobDetailViewModel
            {
                ScheduledJobId = scheduledJob.Id,
                HangfireJobId = string.Empty
            };
        }

        RecurringJobSnapshot? recurringJob = await GetRecurringJobSnapshotAsync(scheduledJob.HangfireJobId);
        HangfireJobExecutionAggregate aggregate = await GetExecutionAggregateAsync(scheduledJob.HangfireJobId);

        return new HangfireJobDetailViewModel
        {
            ScheduledJobId = scheduledJob.Id,
            HangfireJobId = scheduledJob.HangfireJobId,
            ExistsInRecurringJobs = recurringJob?.ExistsInRecurringJobs ?? false,
            SucceededCount = aggregate.SucceededCount,
            FailedCount = aggregate.FailedCount,
            LastExecutedUtc = recurringJob?.LastExecution ?? aggregate.LastExecutedUtc,
            LastSucceededUtc = aggregate.LastSucceededUtc,
            LastFailedUtc = aggregate.LastFailedUtc,
            NextExecutionUtc = recurringJob?.NextExecution,
            LastJobId = recurringJob?.LastJobId,
            LastJobState = recurringJob?.LastJobState,
            LastError = NormalizeEmpty(recurringJob?.Error)
        };
    }

    private async Task<RecurringJobSnapshot?> GetRecurringJobSnapshotAsync(string recurringJobId)
    {
        string schemaName = ResolveHangfireSchemaName();

        //await using var connection = dbContext.Database.GetDbConnection();
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = $@"
SELECT
    CASE
        WHEN EXISTS
        (
            SELECT 1
            FROM [{schemaName}].[Set] SetTable
            WHERE SetTable.[Key] = @recurringJobsSetKey
              AND SetTable.[Value] = @recurringJobId
        )
        THEN CAST(1 AS bit)
        ELSE CAST(0 AS bit)
    END AS ExistsInRecurringJobs,
    MAX(CASE WHEN HashTable.Field = 'LastExecution' THEN HashTable.Value END) AS LastExecution,
    MAX(CASE WHEN HashTable.Field = 'NextExecution' THEN HashTable.Value END) AS NextExecution,
    MAX(CASE WHEN HashTable.Field = 'LastJobId' THEN HashTable.Value END) AS LastJobId,
    MAX(CASE WHEN HashTable.Field = 'LastJobState' THEN HashTable.Value END) AS LastJobState,
    MAX(CASE WHEN HashTable.Field = 'Error' THEN HashTable.Value END) AS LastError
FROM [{schemaName}].[Hash] HashTable
WHERE HashTable.[Key] = @recurringJobHashKey;";

        AddParameter(command, "@recurringJobsSetKey", "recurring-jobs");
        AddParameter(command, "@recurringJobId", recurringJobId);
        AddParameter(command, "@recurringJobHashKey", $"recurring-job:{recurringJobId}");

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        bool existsInRecurringJobs = !reader.IsDBNull(0) && reader.GetBoolean(0);
        //DateTime? lastExecution = reader.IsDBNull(1) ? null : ParseHangfireDateTime(reader.GetString(1));
        DateTime? lastExecution = reader.IsDBNull(1) ? null : ParseHangfireDateTime(long.Parse(reader.GetString(1)));
        DateTime? nextExecution = reader.IsDBNull(2) ? null : ParseHangfireDateTime(long.Parse(reader.GetString(2)));
        string? lastJobId = reader.IsDBNull(3) ? null : NormalizeEmpty(reader.GetString(3));
        string? lastJobState = reader.IsDBNull(4) ? null : NormalizeEmpty(reader.GetString(4));
        string? error = reader.IsDBNull(5) ? null : NormalizeEmpty(reader.GetString(5));

        if (!existsInRecurringJobs &&
            lastExecution is null &&
            nextExecution is null &&
            string.IsNullOrWhiteSpace(lastJobId) &&
            string.IsNullOrWhiteSpace(lastJobState) &&
            string.IsNullOrWhiteSpace(error))
        {
            return null;
        }

        return new RecurringJobSnapshot
        {
            ExistsInRecurringJobs = existsInRecurringJobs,
            LastExecution = lastExecution,
            NextExecution = nextExecution,
            LastJobId = lastJobId,
            LastJobState = lastJobState,
            Error = error
        };
    }

    private static DateTime ParseHangfireDateTime(long unixMilliseconds)
    {
        // Hangfire stores as Unix time in ms
        //return DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds).UtcDateTime;
        return DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds).DateTime.AddHours(8);
    }

    private async Task<HangfireJobExecutionAggregate> GetExecutionAggregateAsync(string recurringJobId)
    {
        string schemaName = ResolveHangfireSchemaName();

        //await using var connection = dbContext.Database.GetDbConnection();
        var connection = dbContext.Database.GetDbConnection();

        try
        {
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }
        }
        catch (Exception ex)
        {

        }

        

        await using var command = connection.CreateCommand();
        command.CommandText = $@"
SELECT
    COUNT(CASE WHEN LatestState.Name = @succeededStateName THEN 1 END) AS SucceededCount,
    COUNT(CASE WHEN LatestState.Name = @failedStateName THEN 1 END) AS FailedCount,
    MAX(CASE WHEN LatestState.Name = @succeededStateName THEN LatestState.CreatedAt END) AS LastSucceededUtc,
    MAX(CASE WHEN LatestState.Name = @failedStateName THEN LatestState.CreatedAt END) AS LastFailedUtc,
    MAX(LatestState.CreatedAt) AS LastExecutedUtc
FROM [{schemaName}].[JobParameter] JobParameter
INNER JOIN [{schemaName}].[Job] Job ON Job.Id = JobParameter.JobId
OUTER APPLY
(
    SELECT TOP (1)
        State.Name,
        State.CreatedAt
    FROM [{schemaName}].[State] State
    WHERE State.JobId = Job.Id
    ORDER BY State.Id DESC
) LatestState
WHERE JobParameter.Name = @recurringJobParameterName
  AND JobParameter.Value = @recurringJobId;";

        AddParameter(command, "@succeededStateName", SucceededStateName);
        AddParameter(command, "@failedStateName", FailedStateName);
        AddParameter(command, "@recurringJobParameterName", RecurringJobIdParameterName);
        AddParameter(command, "@recurringJobId", $"\"{recurringJobId}\"");
        //AddParameter(command, "@recurringJobId", recurringJobId);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return new HangfireJobExecutionAggregate();
        }

        return new HangfireJobExecutionAggregate
        {
            SucceededCount = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
            FailedCount = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
            LastSucceededUtc = reader.IsDBNull(2) ? null : reader.GetDateTime(2),
            LastFailedUtc = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
            LastExecutedUtc = reader.IsDBNull(4) ? null : reader.GetDateTime(4)
        };
    }

    private string ResolveHangfireSchemaName()
    {
        string? configuredSchema = configuration["Hangfire:SchemaName"];
        string normalizedSchema = string.IsNullOrWhiteSpace(configuredSchema)
            ? DefaultHangfireSchemaName
            : configuredSchema.Trim();

        if (ValidSchemaNamePattern.IsMatch(normalizedSchema))
        {
            return normalizedSchema;
        }

        logger.LogWarning(
            "Invalid Hangfire schema name '{SchemaName}' configured. Falling back to {FallbackSchemaName}.",
            configuredSchema,
            DefaultHangfireSchemaName);

        return DefaultHangfireSchemaName;
    }

    private static void AddParameter(IDbCommand command, string parameterName, object value)
    {
        IDbDataParameter parameter = command.CreateParameter();
        parameter.ParameterName = parameterName;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static string? NormalizeEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTime? ParseHangfireDateTime(string rawValue)
    {
        string normalized = rawValue.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset asOffset))
        {
            return asOffset.UtcDateTime;
        }

        if (DateTime.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTime asDateTime))
        {
            return DateTime.SpecifyKind(asDateTime, DateTimeKind.Utc);
        }

        return null;
    }

    private sealed class HangfireJobExecutionAggregate
    {
        public int SucceededCount { get; init; }
        public int FailedCount { get; init; }
        public DateTime? LastExecutedUtc { get; init; }
        public DateTime? LastSucceededUtc { get; init; }
        public DateTime? LastFailedUtc { get; init; }
    }

    private sealed class RecurringJobSnapshot
    {
        public bool ExistsInRecurringJobs { get; init; }
        public DateTime? LastExecution { get; init; }
        public DateTime? NextExecution { get; init; }
        public string? LastJobId { get; init; }
        public string? LastJobState { get; init; }
        public string? Error { get; init; }
    }
}
