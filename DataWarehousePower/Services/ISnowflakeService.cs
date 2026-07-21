using DataWarehousePower.Models;

namespace DataWarehousePower.Services
{
    public interface ISnowflakeService
    {
        Task<IReadOnlyList<Dictionary<string, object?>>> GetAumMasterPreviewAsync(int limit = 10, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Dictionary<string, object?>>> RunQueryAsync(string sql, CancellationToken cancellationToken = default);
        Task<DataFilePreviewResult> GetPreviewDataAsync(DataFilePreviewRequest request, CancellationToken cancellationToken = default);
        Task<List<string>> GetDistinctColumnValuesAsync(
            string? sourceDatabase,
            string? sourceTable,
            string? sourceSP,
            string? columnName,
            string? search = null,
            int take = 50,
            CancellationToken cancellationToken = default);
    }
}
