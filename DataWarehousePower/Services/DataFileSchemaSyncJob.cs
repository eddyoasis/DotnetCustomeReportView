using DataWarehousePower.Models;
using DataWarehousePower.Models.AppSettings;
using DataWarehousePower.Repositories;
using Hangfire;
using Microsoft.Extensions.Options;

namespace DataWarehousePower.Services;

public sealed class DataFileSchemaSyncJob(
    IDataFileManageRepository dataFileManageRepository,
    IDataFileManageService dataFileManageService,
    IDepartmentService departmentService,
    ISnowflakeService snowflakeService,
    IOptionsSnapshot<ScheduledJob> scheduledJobOptions,
    ILogger<DataFileSchemaSyncJob> logger) : IDataFileSchemaSyncJob
{
    [AutomaticRetry(Attempts = 2)]
    public async Task SyncAllDataFilesAsync()
    {
        if (!scheduledJobOptions.Value.UseSnowflakeForDataFile)
        {
            logger.LogInformation("Data file schema sync job skipped because ScheduledJob:UseSnowflakeForDataFile is disabled.");
            return;
        }

        List<DataFileDefinition> dataFiles = await dataFileManageRepository.GetAllWithColumnsAsync();
        if (dataFiles.Count == 0)
        {
            logger.LogInformation("Data file schema sync job found no data files to process.");
            return;
        }

        Dictionary<int, string> departmentNameById = (await departmentService.GetAllAsync())
            .Where(department => department.IsActive)
            .GroupBy(department => department.Id)
            .ToDictionary(group => group.Key, group => group.First().Name);

        List<string> allActiveDepartments = departmentNameById
            .Select(pair => pair.Value)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        int syncedCount = 0;
        int skippedCount = 0;

        foreach (DataFileDefinition dataFile in dataFiles)
        {
            if (string.IsNullOrWhiteSpace(dataFile.SourceTable) || !string.IsNullOrWhiteSpace(dataFile.SourceSP))
            {
                skippedCount++;
                continue;
            }

            List<string> candidateDepartments = ResolveCandidateDepartments(dataFile.Departments, departmentNameById, allActiveDepartments);
            if (candidateDepartments.Count == 0)
            {
                skippedCount++;
                logger.LogWarning("Data file schema sync skipped data file {DataFileId} because no candidate department could be resolved.", dataFile.Id);
                continue;
            }

            bool synced = false;
            var departmentName = "Information Technology";

            try
            {
                List<SourceColumnMetadata> sourceColumns = await snowflakeService.GetSourceColumnMetadataAsync(
                    departmentName,
                    dataFile.SourceDatabase,
                    dataFile.SourceTable,
                    dataFile.SourceSP);

                if (sourceColumns.Count == 0)
                {
                    continue;
                }

                await dataFileManageService.SyncDataFileColumnsAsync(dataFile.Id, sourceColumns);
                syncedCount++;
                synced = true;
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Data file schema sync failed for data file {DataFileId} using department {DepartmentName}.",
                    dataFile.Id,
                    departmentName);
            }

            if (!synced)
            {
                skippedCount++;
            }
        }

        logger.LogInformation(
            "Data file schema sync job completed. Total={TotalCount}, Synced={SyncedCount}, Skipped={SkippedCount}.",
            dataFiles.Count,
            syncedCount,
            skippedCount);
    }

    private static List<string> ResolveCandidateDepartments(
        string? departments,
        IReadOnlyDictionary<int, string> departmentNameById,
        IReadOnlyCollection<string> fallbackDepartments)
    {
        List<string> resolved = new();

        foreach (string token in (departments ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(token, out int departmentId))
            {
                if (departmentNameById.TryGetValue(departmentId, out string? departmentName) && !string.IsNullOrWhiteSpace(departmentName))
                {
                    resolved.Add(departmentName);
                }

                continue;
            }

            if (!string.IsNullOrWhiteSpace(token))
            {
                resolved.Add(token);
            }
        }

        return resolved
            .Concat(fallbackDepartments)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
