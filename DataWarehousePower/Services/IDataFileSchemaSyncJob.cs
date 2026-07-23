namespace DataWarehousePower.Services;

public interface IDataFileSchemaSyncJob
{
    Task SyncAllDataFilesAsync();
}
