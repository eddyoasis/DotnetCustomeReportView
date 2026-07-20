using DataWarehousePower.Repositories;
using DataWarehousePower.Models;
using System.Text.RegularExpressions;

namespace DataWarehousePower.Services
{
    public class SnowflakeService : ISnowflakeService
    {
        private static readonly Regex _identifierPattern = new("^[A-Za-z0-9_]+$", RegexOptions.Compiled);
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

        public async Task<DataFilePreviewResult> GetPreviewDataAsync(DataFilePreviewRequest request, CancellationToken cancellationToken = default)
        {
            if (request is null)
            {
                throw new ArgumentException("Preview request is required.", nameof(request));
            }

            string sourceTable = (request.SourceTable ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(sourceTable))
            {
                throw new ArgumentException("Source table is required for preview.", nameof(request));
            }

            string tableExpression = BuildTableExpression(request.SourceDatabase, sourceTable);

            List<string> selectedColumns = (request.Columns ?? new List<DataFilePreviewColumnRequest>())
                .Where(column => !string.IsNullOrWhiteSpace(column.PropertyName))
                .Select(column => column.PropertyName.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (selectedColumns.Count == 0)
            {
                throw new ArgumentException("At least one preview column is required.", nameof(request));
            }

            string selectColumns = string.Join(", ", selectedColumns.Select(QuoteIdentifier));

            int take = request.Take;
            int page = request.Page <= 0 ? 1 : request.Page;
            if (request.IsExport)
            {
                take = 1000000;
            }
            else
            {
                if (take <= 0)
                {
                    take = 10;
                }
                else if (take != 10 && take != 25 && take != 50 && take != 100)
                {
                    take = 10;
                }
            }

            int offset = (page - 1) * take;

            string whereClause = string.Empty;
            string clientCode = (request.ClientCode ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(clientCode))
            {
                DataFilePreviewColumnRequest? clientCodeColumn = (request.Columns ?? new List<DataFilePreviewColumnRequest>())
                    .FirstOrDefault(column =>
                        string.Equals(column.MappingParameter?.Trim(), "ClientCode", StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(column.PropertyName));

                if (clientCodeColumn is not null)
                {
                    string escapedClientCode = clientCode.Replace("'", "''", StringComparison.Ordinal);
                    whereClause = $" WHERE CAST({QuoteIdentifier(clientCodeColumn.PropertyName.Trim())} AS STRING) ILIKE '%{escapedClientCode}%'";
                }
            }

            //string countSql = $"SELECT COUNT(1) AS TOTAL_COUNT FROM {tableExpression}{whereClause}";
            string countSql = $"SELECT TOP 10 * FROM VW_AUM_MASTER;";
            IReadOnlyList<Dictionary<string, object?>> countRows = await _snowflakeRepository.ExecuteQueryAsync(countSql, cancellationToken);
            int totalCount = 0;
            if (countRows.Count > 0 && countRows[0].TryGetValue("TOTAL_COUNT", out object? totalCountObj) && totalCountObj is not null)
            {
                totalCount = Convert.ToInt32(totalCountObj);
            }

            string querySql =
                $"SELECT {selectColumns} FROM {tableExpression}{whereClause} LIMIT {take} OFFSET {offset}";

            IReadOnlyList<Dictionary<string, object?>> rows = await _snowflakeRepository.ExecuteQueryAsync(querySql, cancellationToken);

            return new DataFilePreviewResult
            {
                Columns = selectedColumns,
                Rows = rows.ToList(),
                TotalRowCount = totalCount
            };
        }

        private static string BuildTableExpression(string? sourceDatabase, string sourceTable)
        {
            string table = sourceTable.Trim();
            if (table.Contains('.'))
            {
                return string.Join('.', table.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(QuoteIdentifier));
            }

            string database = (sourceDatabase ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(database))
            {
                return QuoteIdentifier(table);
            }

            return $"{QuoteIdentifier(database)}.{QuoteIdentifier(table)}";
        }

        private static string QuoteIdentifier(string value)
        {
            string trimmed = value.Trim();
            if (!_identifierPattern.IsMatch(trimmed))
            {
                throw new ArgumentException($"Invalid identifier '{value}'.");
            }

            return $"\"{trimmed}\"";
        }
    }
}
