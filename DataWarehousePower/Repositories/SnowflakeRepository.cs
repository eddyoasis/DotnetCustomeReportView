using Snowflake.Data.Client;
using System.Data;

namespace DataWarehousePower.Repositories
{
    public class SnowflakeRepository : ISnowflakeRepository
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SnowflakeRepository> _logger;

        public SnowflakeRepository(IConfiguration configuration, ILogger<SnowflakeRepository> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<IReadOnlyList<Dictionary<string, object?>>> ExecuteQueryAsync(string connectionString, string sql, CancellationToken cancellationToken = default)
        {
            await using var connection = new SnowflakeDbConnection
            {
                ConnectionString = connectionString
            };

            try
            {
                await connection.OpenAsync(cancellationToken);

                using IDbCommand command = connection.CreateCommand();
                command.CommandText = sql;

                using IDataReader reader = await ((SnowflakeDbCommand)command).ExecuteReaderAsync(cancellationToken);

                var rows = new List<Dictionary<string, object?>>();
                while (reader.Read())
                {
                    var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        object value = reader.GetValue(i);
                        row[reader.GetName(i)] = value == DBNull.Value ? null : value;
                    }

                    rows.Add(row);
                }

                return rows;
            }
            catch (SnowflakeDbException ex)
            {
                _logger.LogError(ex, "Snowflake query failed.");
                throw;
            }
        }

        public async Task<IReadOnlyList<Dictionary<string, object?>>> ExecuteQueryAsync(string sql, CancellationToken cancellationToken = default)
        {
            string connectionString = _configuration.GetConnectionString("Snowflake")
                ?? throw new InvalidOperationException("Connection string 'Snowflake' is missing.");

            await using var connection = new SnowflakeDbConnection
            {
                ConnectionString = connectionString
            };

            try
            {
                await connection.OpenAsync(cancellationToken);

                using IDbCommand command = connection.CreateCommand();
                command.CommandText = sql;

                using IDataReader reader = await ((SnowflakeDbCommand)command).ExecuteReaderAsync(cancellationToken);

                var rows = new List<Dictionary<string, object?>>();
                while (reader.Read())
                {
                    var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        object value = reader.GetValue(i);
                        row[reader.GetName(i)] = value == DBNull.Value ? null : value;
                    }

                    rows.Add(row);
                }

                return rows;
            }
            catch (SnowflakeDbException ex)
            {
                _logger.LogError(ex, "Snowflake query failed.");
                throw;
            }
        }
    }
}
