using DataWarehousePower.Models;

namespace DataWarehousePower.Services
{
    public interface ISnowflakeService
    {
        Task<List<string>> GetSourceDatabaseOptionsAsync(string? userDepartment, CancellationToken cancellationToken = default);
        Task<List<string>> GetSourceTableOptionsAsync(string? userDepartment, string? sourceDatabase, CancellationToken cancellationToken = default);
        Task<List<string>> GetSourceStoredProcedureOptionsAsync(string? userDepartment, string? sourceDatabase, CancellationToken cancellationToken = default);
        Task<List<string>> GetSourceColumnsAsync(string? userDepartment, string? sourceDatabase, string? sourceTable, string? sourceSP, CancellationToken cancellationToken = default);
        Task<List<SourceColumnMetadata>> GetSourceColumnMetadataAsync(string? userDepartment, string? sourceDatabase, string? sourceTable, string? sourceSP, CancellationToken cancellationToken = default);
        Task<List<string>> GetSourceParametersAsync(string? userDepartment, string? sourceDatabase, string? sourceTable, string? sourceSP, CancellationToken cancellationToken = default);
        Task<DataFilePreviewResult> GetPreviewDataAsync(DataFilePreviewRequest request, CancellationToken cancellationToken = default);
        Task<List<string>> GetDistinctColumnValuesAsync(
            string? userDepartment,
            string? sourceDatabase,
            string? sourceTable,
            string? sourceSP,
            string? columnName,
            string? search = null,
            int take = 50,
            CancellationToken cancellationToken = default);
    }
}
