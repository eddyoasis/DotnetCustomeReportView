using DataWarehousePower.Repositories;

namespace DataWarehousePower.Services
{
    public class SnowflakeService : ISnowflakeService
    {
        private readonly ISnowflakeRepository _snowflakeRepository;

        public SnowflakeService(ISnowflakeRepository snowflakeRepository)
        {
            _snowflakeRepository = snowflakeRepository;
        }

        public Task<IReadOnlyList<Dictionary<string, object?>>> GetAumMasterPreviewAsync(int limit = 10, CancellationToken cancellationToken = default)
        {
            if (limit < 1 || limit > 1000)
            {
                throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 1000.");
            }

            string sql = $"SELECT * FROM VW_AUM_MASTER LIMIT {limit}";
            return _snowflakeRepository.ExecuteQueryAsync(sql, cancellationToken);
        }

        public Task<IReadOnlyList<Dictionary<string, object?>>> RunQueryAsync(string sql, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sql))
            {
                throw new ArgumentException("SQL cannot be empty.", nameof(sql));
            }

            return _snowflakeRepository.ExecuteQueryAsync(sql, cancellationToken);
        }
    }
}
