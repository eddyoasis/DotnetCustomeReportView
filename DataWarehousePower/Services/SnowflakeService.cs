using DataWarehousePower.Repositories;
using DataWarehousePower.Models;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DataWarehousePower.Services
{
    public class SnowflakeService : ISnowflakeService
    {
        private static readonly Regex _identifierPattern = new("^[A-Za-z0-9_]+$", RegexOptions.Compiled);
        private readonly ISnowflakeRepository _snowflakeRepository;
        private readonly IDepartmentSnowflakeConnectionService _departmentSnowflakeConnectionService;

        public SnowflakeService(
            IDepartmentSnowflakeConnectionService departmentSnowflakeConnectionService,
            ISnowflakeRepository snowflakeRepository)
        {
            _snowflakeRepository = snowflakeRepository;
            _departmentSnowflakeConnectionService = departmentSnowflakeConnectionService;
        }

        public async Task<DataFilePreviewResult> GetPreviewDataAsync(DataFilePreviewRequest request, CancellationToken cancellationToken = default)
        {
            if (request is null)
            {
                throw new ArgumentException("Preview request is required.", nameof(request));
            }

            if (string.IsNullOrEmpty(request.UserDepartment))
            {
                throw new ArgumentException("Preview request UserDepartment is required.", nameof(request.UserDepartment));
            }

            var snowflakeConnectionString = await _departmentSnowflakeConnectionService.GetConnectionStringByUserDepartmentAsync(request.UserDepartment);
            if (string.IsNullOrEmpty(snowflakeConnectionString))
            {
                throw new ArgumentException($"No Snowflake connection is configured for your department({request.UserDepartment}). Kindly reach out to the administrator for support.");
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

            bool hasExplicitParameters = request.Parameters is not null && request.Parameters.Count > 0;
            Dictionary<string, string?> parameters = (request.Parameters ?? new Dictionary<string, string?>())
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
                .ToDictionary(
                    pair => pair.Key.Trim(),
                    pair => pair.Value?.Trim(),
                    StringComparer.OrdinalIgnoreCase);

            var whereClauses = new List<string>();
            foreach (DataFilePreviewColumnRequest requestedColumn in (request.Columns ?? new List<DataFilePreviewColumnRequest>())
                         .Where(column => !string.IsNullOrWhiteSpace(column.PropertyName))
                         .GroupBy(column => column.PropertyName.Trim(), StringComparer.OrdinalIgnoreCase)
                         .Select(group => group.First()))
            {
                string columnName = requestedColumn.PropertyName.Trim();

                if (IsClientCodeMapping(requestedColumn.MappingParameter))
                {
                    string clientCode = (request.ClientCode ?? string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(clientCode))
                    {
                        whereClauses.Add($"CAST({QuoteIdentifier(columnName)} AS STRING) ILIKE '%{EscapeSqlLiteral(clientCode)}%'");
                    }

                    continue;
                }

                string parameterKey = (requestedColumn.MappingParameter ?? string.Empty).Trim();
                string filterValue = string.Empty;

                if (hasExplicitParameters && !string.IsNullOrWhiteSpace(parameterKey) && parameters.TryGetValue(parameterKey, out string? parameterFilterValue))
                {
                    filterValue = (parameterFilterValue ?? string.Empty).Trim();
                }

                if (hasExplicitParameters && string.IsNullOrWhiteSpace(parameterKey) && parameters.TryGetValue(columnName, out string? parameterColumnFilterValue))
                {
                    filterValue = (parameterColumnFilterValue ?? string.Empty).Trim();
                }

                if (!hasExplicitParameters && string.IsNullOrWhiteSpace(filterValue) && !string.IsNullOrWhiteSpace(parameterKey))
                {
                    filterValue = parameterKey;
                }

                if (string.IsNullOrWhiteSpace(filterValue))
                {
                    continue;
                }

                if (TryParseDateTimeRangeFilter(filterValue, out DateTime? startDateTime, out DateTime? endDateTime))
                {
                    string timestampExpression = $"TRY_TO_TIMESTAMP_NTZ(CAST({QuoteIdentifier(columnName)} AS STRING))";

                    if (startDateTime.HasValue)
                    {
                        whereClauses.Add($"{timestampExpression} >= {ToTimestampLiteral(startDateTime.Value)}");
                    }

                    if (endDateTime.HasValue)
                    {
                        whereClauses.Add($"{timestampExpression} <= {ToTimestampLiteral(endDateTime.Value)}");
                    }

                    continue;
                }

                if (TryParseMultiSelectFilter(filterValue, out List<string> selectedValues))
                {
                    whereClauses.Add(BuildMultiSelectStringClause(columnName, selectedValues));
                    continue;
                }

                if (TryParseDecimalRangeFilter(filterValue, out decimal? fromValue, out decimal? toValue))
                {
                    string decimalExpression = $"TRY_TO_DECIMAL(CAST({QuoteIdentifier(columnName)} AS STRING), 38, 10)";

                    if (fromValue.HasValue)
                    {
                        whereClauses.Add($"{decimalExpression} >= {ToDecimalLiteral(fromValue.Value)}");
                    }

                    if (toValue.HasValue)
                    {
                        whereClauses.Add($"{decimalExpression} <= {ToDecimalLiteral(toValue.Value)}");
                    }

                    continue;
                }

                if (TryParseIntegerFilter(filterValue, out long intValue))
                {
                    whereClauses.Add($"TRY_TO_NUMBER(CAST({QuoteIdentifier(columnName)} AS STRING), 38, 0) = {intValue.ToString(CultureInfo.InvariantCulture)}");
                    continue;
                }

                if (TryParseBooleanFilter(filterValue, out bool boolValue))
                {
                    whereClauses.Add($"TRY_TO_BOOLEAN(CAST({QuoteIdentifier(columnName)} AS STRING)) = {(boolValue ? "TRUE" : "FALSE")}");
                    continue;
                }

                whereClauses.Add($"CAST({QuoteIdentifier(columnName)} AS STRING) ILIKE '%{EscapeSqlLiteral(filterValue)}%'");
            }

            string whereClause = whereClauses.Count > 0
                ? " WHERE " + string.Join(" AND ", whereClauses)
                : string.Empty;

            string countSql = $"SELECT COUNT(1) AS TOTAL_COUNT FROM {tableExpression}{whereClause}";
            //IReadOnlyList<Dictionary<string, object?>> countRows = await _snowflakeRepository.ExecuteQueryAsync(countSql, cancellationToken);
            
            IReadOnlyList<Dictionary<string, object?>> countRows = await _snowflakeRepository.ExecuteQueryAsync(snowflakeConnectionString, countSql, cancellationToken);
            int totalCount = 0;
            if (countRows.Count > 0 && countRows[0].TryGetValue("TOTAL_COUNT", out object? totalCountObj) && totalCountObj is not null)
            {
                totalCount = Convert.ToInt32(totalCountObj, CultureInfo.InvariantCulture);
            }

            string querySql =
                $"SELECT {selectColumns} FROM {tableExpression}{whereClause} LIMIT {take} OFFSET {offset}";

            IReadOnlyList<Dictionary<string, object?>> rows = await _snowflakeRepository.ExecuteQueryAsync(snowflakeConnectionString, querySql, cancellationToken);
            //IReadOnlyList<Dictionary<string, object?>> rows = await _snowflakeRepository.ExecuteQueryAsync(querySql, cancellationToken);

            return new DataFilePreviewResult
            {
                Columns = selectedColumns,
                Rows = rows.ToList(),
                TotalRowCount = totalCount
            };
        }

        public async Task<List<string>> GetDistinctColumnValuesAsync(
            string? userDepartment,
            string? sourceDatabase,
            string? sourceTable,
            string? sourceSP,
            string? columnName,
            string? search = null,
            int take = 50,
            CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrWhiteSpace(sourceSP))
            {
                throw new InvalidOperationException("Snowflake filter values currently supports Source Table only.");
            }

            if (string.IsNullOrWhiteSpace(userDepartment))
            {
                throw new ArgumentException("userDepartment is required.", nameof(userDepartment));
            }

            string normalizedTable = (sourceTable ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedTable))
            {
                throw new ArgumentException("Source table is required.", nameof(sourceTable));
            }

            string normalizedColumn = (columnName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedColumn))
            {
                throw new ArgumentException("Column name is required.", nameof(columnName));
            }

            int effectiveTake = take <= 0 ? 50 : Math.Min(take, 200);
            string tableExpression = BuildTableExpression(sourceDatabase, normalizedTable);
            string escapedSearch = EscapeSqlLiteral((search ?? string.Empty).Trim());
            string columnExpression = $"CAST({QuoteIdentifier(normalizedColumn)} AS STRING)";

            string searchClause = string.IsNullOrWhiteSpace(escapedSearch)
                ? string.Empty
                : $" AND {columnExpression} ILIKE '%{escapedSearch}%'";

            string sql =
                $"SELECT DISTINCT {columnExpression} AS VALUE " +
                $"FROM {tableExpression} " +
                $"WHERE {QuoteIdentifier(normalizedColumn)} IS NOT NULL{searchClause} " +
                "ORDER BY VALUE " +
                $"LIMIT {effectiveTake}";

            var snowflakeConnectionString = await _departmentSnowflakeConnectionService.GetConnectionStringByUserDepartmentAsync(userDepartment);
            IReadOnlyList<Dictionary<string, object?>> rows = await _snowflakeRepository.ExecuteQueryAsync(snowflakeConnectionString, sql, cancellationToken);

            return rows
                .Select(row => row.TryGetValue("VALUE", out object? value) ? value?.ToString() : null)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool IsClientCodeMapping(string? mappingParameter)
        {
            string normalized = (mappingParameter ?? string.Empty).Trim();
            return !string.IsNullOrWhiteSpace(normalized)
                && (normalized.Contains("ClientCode", StringComparison.OrdinalIgnoreCase)
                    || normalized.Contains("ClintCode", StringComparison.OrdinalIgnoreCase));
        }

        private static bool TryParseDateTimeRangeFilter(string value, out DateTime? start, out DateTime? end)
        {
            start = null;
            end = null;

            string raw = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return true;
            }

            if (raw.Contains('|', StringComparison.Ordinal))
            {
                string[] parts = raw.Split('|', 2, StringSplitOptions.None);

                if (!string.IsNullOrWhiteSpace(parts[0]))
                {
                    if (!TryParseDateTime(parts[0], out DateTime parsedStart))
                    {
                        return false;
                    }

                    start = parsedStart;
                }

                if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
                {
                    if (!TryParseDateTime(parts[1], out DateTime parsedEnd))
                    {
                        return false;
                    }

                    end = parsedEnd;
                }

                return true;
            }

            if (!TryParseDateTime(raw, out DateTime singleValue))
            {
                return false;
            }

            start = singleValue;
            end = singleValue;
            return true;
        }

        private static bool TryParseDateTime(string value, out DateTime parsed)
        {
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out parsed))
            {
                return true;
            }

            return DateTime.TryParse(value, out parsed);
        }

        private static bool TryParseDecimalRangeFilter(string value, out decimal? from, out decimal? to)
        {
            from = null;
            to = null;

            string raw = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return true;
            }

            if (raw.Contains('|', StringComparison.Ordinal))
            {
                string[] parts = raw.Split('|', 2, StringSplitOptions.None);

                if (!string.IsNullOrWhiteSpace(parts[0]))
                {
                    if (!decimal.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out decimal parsedFrom) &&
                        !decimal.TryParse(parts[0], NumberStyles.Any, CultureInfo.CurrentCulture, out parsedFrom))
                    {
                        return false;
                    }

                    from = parsedFrom;
                }

                if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
                {
                    if (!decimal.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out decimal parsedTo) &&
                        !decimal.TryParse(parts[1], NumberStyles.Any, CultureInfo.CurrentCulture, out parsedTo))
                    {
                        return false;
                    }

                    to = parsedTo;
                }

                return true;
            }

            if (!decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal singleValue) &&
                !decimal.TryParse(raw, NumberStyles.Any, CultureInfo.CurrentCulture, out singleValue))
            {
                return false;
            }

            from = singleValue;
            to = singleValue;
            return true;
        }

        private static bool TryParseIntegerFilter(string value, out long parsed)
        {
            string raw = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                parsed = 0;
                return false;
            }

            if (raw.Contains('|', StringComparison.Ordinal))
            {
                string[] parts = raw.Split('|', 2, StringSplitOptions.None);
                raw = !string.IsNullOrWhiteSpace(parts[0]) ? parts[0].Trim() : (parts.Length > 1 ? parts[1].Trim() : string.Empty);
            }

            return long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ||
                long.TryParse(raw, NumberStyles.Integer, CultureInfo.CurrentCulture, out parsed);
        }

        private static bool TryParseBooleanFilter(string value, out bool parsed)
        {
            string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                parsed = false;
                return false;
            }

            if (normalized is "1" or "true" or "yes" or "y")
            {
                parsed = true;
                return true;
            }

            if (normalized is "0" or "false" or "no" or "n")
            {
                parsed = false;
                return true;
            }

            return bool.TryParse(normalized, out parsed);
        }

        private static bool TryParseMultiSelectFilter(string value, out List<string> selectedValues)
        {
            selectedValues = new List<string>();
            string normalized = (value ?? string.Empty).Trim();

            if (normalized.StartsWith("[", StringComparison.Ordinal) &&
                normalized.EndsWith("]", StringComparison.Ordinal))
            {
                try
                {
                    List<string>? parsed = JsonSerializer.Deserialize<List<string>>(normalized);
                    if (parsed is not null)
                    {
                        selectedValues = parsed
                            .Where(item => !string.IsNullOrWhiteSpace(item))
                            .Select(item => item.Trim())
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();

                        if (selectedValues.Count > 0)
                        {
                            return true;
                        }
                    }
                }
                catch (JsonException)
                {
                }
            }

            if (!normalized.Contains(',', StringComparison.Ordinal) &&
                !normalized.Contains(';', StringComparison.Ordinal) &&
                !normalized.Contains('|', StringComparison.Ordinal))
            {
                return false;
            }

            selectedValues = normalized
                .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return selectedValues.Count > 1;
        }

        private static string BuildMultiSelectStringClause(string columnName, IEnumerable<string> selectedValues)
        {
            List<string> predicates = new();
            string escapedColumnName = QuoteIdentifier(columnName);

            foreach (string selectedValue in selectedValues)
            {
                predicates.Add($"CAST({escapedColumnName} AS STRING) = '{EscapeSqlLiteral(selectedValue)}'");
            }

            return "(" + string.Join(" OR ", predicates) + ")";
        }

        private static string EscapeSqlLiteral(string value)
            => value.Replace("'", "''", StringComparison.Ordinal);

        private static string ToTimestampLiteral(DateTime value)
            => $"TO_TIMESTAMP_NTZ('{value.ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture)}')";

        private static string ToDecimalLiteral(decimal value)
            => value.ToString(CultureInfo.InvariantCulture);

        private static string BuildTableExpression(string? sourceDatabase, string sourceTable)
        {
            return sourceTable.Replace("TBL_", "");
        }

        //private static string BuildTableExpression(string? sourceDatabase, string sourceTable)
        //{
        //    string table = sourceTable.Trim();
        //    if (table.Contains('.'))
        //    {
        //        return string.Join('.', table.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(QuoteIdentifier));
        //    }

        //    string database = (sourceDatabase ?? string.Empty).Trim();
        //    if (string.IsNullOrWhiteSpace(database))
        //    {
        //        return QuoteIdentifier(table);
        //    }

        //    return $"{QuoteIdentifier(database)}.{QuoteIdentifier(table)}";
        //}

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
