namespace DataWarehousePower.Repositories
{
    public interface ISnowflakeRepository
    {
        Task<IReadOnlyList<Dictionary<string, object?>>> ExecuteQueryAsync(string sql, CancellationToken cancellationToken = default);
    }
}
