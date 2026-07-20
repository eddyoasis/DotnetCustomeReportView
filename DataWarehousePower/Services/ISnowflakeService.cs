using DataWarehousePower.Models;

namespace DataWarehousePower.Services
{
    public interface ISnowflakeService
    {
        Task<IReadOnlyList<Dictionary<string, object?>>> GetAumMasterPreviewAsync(int limit = 10, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Dictionary<string, object?>>> RunQueryAsync(string sql, CancellationToken cancellationToken = default);
        Task<DataFilePreviewResult> GetPreviewDataAsync(DataFilePreviewRequest request, CancellationToken cancellationToken = default);
    }
}
