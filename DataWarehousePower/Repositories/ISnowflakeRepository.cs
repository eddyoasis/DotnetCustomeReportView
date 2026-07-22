namespace DataWarehousePower.Repositories
{
    public interface ISnowflakeRepository
    {
        Task<IReadOnlyList<Dictionary<string, object?>>> ExecuteQueryAsync(string connectionString, string sql, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Dictionary<string, object?>>> ExecuteQueryAsync(string sql, CancellationToken cancellationToken = default);
    }
}
