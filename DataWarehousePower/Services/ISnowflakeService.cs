using DataWarehousePower.Models;

namespace DataWarehousePower.Services
{
    public interface ISnowflakeService
    {
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
