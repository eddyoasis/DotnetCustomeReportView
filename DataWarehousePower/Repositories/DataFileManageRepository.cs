using DataWarehousePower.Data;
using DataWarehousePower.Helper;
using DataWarehousePower.Models;
using DataWarehousePower.Models.AppSettings;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text.Json;

namespace DataWarehousePower.Repositories
{
    public class DataFileManageRepository : IDataFileManageRepository
    {
        private readonly AppDbContext _context;
        private readonly ClientCodeLookupOptions _clientCodeLookupOptions;
        private readonly ScheduledJob _scheduledJob;


        public DataFileManageRepository(
            IOptionsSnapshot<ClientCodeLookupOptions> clientCodeLookupOptions,
            IOptionsSnapshot<ScheduledJob> scheduledJob,
            AppDbContext context)
        {
            _context = context;
            _scheduledJob = scheduledJob.Value;
            _clientCodeLookupOptions = clientCodeLookupOptions.Value;

        }

        public async Task<List<string>> GetSourceDatabaseAsync(string dbConnectionString)
        {
            await using var conn = new SqlConnection(dbConnectionString);
            if (conn.State != ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT name " +
                "FROM sys.databases " +
                "WHERE state_desc = 'ONLINE' AND HAS_DBACCESS(name) = 1 " +
                "ORDER BY name";

            var items = new List<string>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (!reader.IsDBNull(0))
                {
                    items.Add(reader.GetString(0));
                }
            }

            return items.Where(x => x == conn.Database).ToList();
        }

        public async Task<List<string>> GetSourceDatabaseOptionsAsync(string dbConnectionString)
        {
            await using var conn = new SqlConnection(dbConnectionString);
            if (conn.State != ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT name " +
                "FROM sys.databases " +
                "WHERE state_desc = 'ONLINE' AND HAS_DBACCESS(name) = 1 " +
                "ORDER BY name";

            var items = new List<string>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (!reader.IsDBNull(0))
                {
                    items.Add(reader.GetString(0));
                }
            }

            return items;
        }

        public async Task<List<string>> GetSourceDatabaseOptionsAsync()
        {
            var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT name " +
                "FROM sys.databases " +
                "WHERE state_desc = 'ONLINE' AND HAS_DBACCESS(name) = 1 " +
                "ORDER BY name";

            var items = new List<string>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (!reader.IsDBNull(0))
                {
                    items.Add(reader.GetString(0));
                }
            }

            return items;
        }

        public async Task<List<string>> GetSourceTableOptionsAsync(string dbConnectionString, string? sourceDatabase)
        {
            if (string.IsNullOrWhiteSpace(sourceDatabase))
            {
                return new();
            }

            await using var conn = new SqlConnection(dbConnectionString);
            if (conn.State != ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

            var escapedDatabase = EscapeSqlIdentifier(sourceDatabase.Trim());

            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT DISTINCT TABLE_NAME " +
                "FROM [" + escapedDatabase + "].INFORMATION_SCHEMA.TABLES " +
                "WHERE TABLE_TYPE IN ('BASE TABLE', 'VIEW') " +
                "ORDER BY TABLE_NAME";

            var items = new List<string>();
            try
            {
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(0))
                    {
                        items.Add(reader.GetString(0));
                    }
                }
            }
            catch (SqlException ex) when (ex.Number is 916 or 229 or 911)
            {
                return new();
            }

            return items;
        }

        public async Task<List<string>> GetSourceTableOptionsAsync(string? sourceDatabase)
        {
            if (string.IsNullOrWhiteSpace(sourceDatabase))
            {
                return new();
            }

            var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

            var escapedDatabase = EscapeSqlIdentifier(sourceDatabase.Trim());

            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT DISTINCT TABLE_NAME " +
                "FROM [" + escapedDatabase + "].INFORMATION_SCHEMA.TABLES " +
                "WHERE TABLE_TYPE IN ('BASE TABLE', 'VIEW') " +
                "ORDER BY TABLE_NAME";

            var items = new List<string>();
            try
            {
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(0))
                    {
                        items.Add(reader.GetString(0));
                    }
                }
            }
            catch (SqlException ex) when (ex.Number is 916 or 229 or 911)
            {
                return new();
            }

            return items;
        }

        public async Task<List<string>> GetSourceStoredProcedureOptionsAsync(string dbConnectionString, string? sourceDatabase)
        {
            if (string.IsNullOrWhiteSpace(sourceDatabase))
            {
                return new();
            }

            await using var conn = new SqlConnection(dbConnectionString);
            if (conn.State != ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

            var escapedDatabase = EscapeSqlIdentifier(sourceDatabase.Trim());

            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT name " +
                "FROM [" + escapedDatabase + "].sys.procedures " +
                "WHERE is_ms_shipped = 0 " +
                "ORDER BY name";

            var items = new List<string>();
            try
            {
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(0))
                    {
                        items.Add(reader.GetString(0));
                    }
                }
            }
            catch (SqlException ex) when (ex.Number is 916 or 229 or 911)
            {
                return new();
            }

            return items;
        }

        public async Task<List<string>> GetSourceStoredProcedureOptionsAsync(string? sourceDatabase)
        {
            if (string.IsNullOrWhiteSpace(sourceDatabase))
            {
                return new();
            }

            var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

            var escapedDatabase = EscapeSqlIdentifier(sourceDatabase.Trim());

            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT name " +
                "FROM [" + escapedDatabase + "].sys.procedures " +
                "WHERE is_ms_shipped = 0 " +
                "ORDER BY name";

            var items = new List<string>();
            try
            {
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(0))
                    {
                        items.Add(reader.GetString(0));
                    }
                }
            }
            catch (SqlException ex) when (ex.Number is 916 or 229 or 911)
            {
                return new();
            }

            return items;
        }

        public async Task<List<string>> GetSourceColumnsAsync(string? sourceDatabase, string? sourceTable, string? sourceSP)
            => (await GetSourceColumnMetadataAsync(sourceDatabase, sourceTable, sourceSP))
                .Select(column => column.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();

        public async Task<DataFilePreviewResult> GetPreviewDataAsync(
            DataFilePreviewRequest request,
            string? filterClientCodeColumn,
            string? filterDateColumn,
            DateTime? dateFrom,
            DateTime? dateTo)
        {
            if (request is null)
            {
                throw new ArgumentException("Preview request is required.");
            }

            var clintCodes = await GetTRIDsByUserIdAsync(request?.UserId);
            var clintCodesForSql = $"({string.Join(",", clintCodes.Select(code => $"'{code}'"))})";

            string sourceDatabase = (request.SourceDatabase ?? string.Empty).Trim();
            string sourceTable = (request.SourceTable ?? string.Empty).Trim();
            string sourceSP = (request.SourceSP ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(sourceDatabase))
            {
                throw new ArgumentException("Source database is required for preview.");
            }

            if (!string.IsNullOrWhiteSpace(sourceSP))
            {
                throw new InvalidOperationException("Preview currently supports Source Table only.");
            }

            if (string.IsNullOrWhiteSpace(sourceTable))
            {
                throw new ArgumentException("Source table is required for preview.");
            }

            int take = request.Take;
            int page = request.Page <= 0 ? 1 : request.Page;
            if (request.IsExport)
            {
                take = _scheduledJob.ExportSplit.MaxTotalRecord;
            }
            else
            {
                if (take <= 0)
                {
                    take = 0;
                }
                else if (take != 10 && take != 25 && take != 50 && take != 100)
                {
                    take = 10;
                }
            }

            var metadata = await GetSourceColumnMetadataAsync(sourceDatabase, sourceTable, null);
            if (metadata.Count == 0)
            {
                return new DataFilePreviewResult();
            }

            var metadataByName = metadata
                .Where(column => !string.IsNullOrWhiteSpace(column.Name))
                .ToDictionary(column => column.Name, StringComparer.OrdinalIgnoreCase);
            bool hasExplicitParameters = request.Parameters is not null && request.Parameters.Count() > 0;
            var parameters = (request.Parameters ?? new Dictionary<string, string?>())
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
                .ToDictionary(
                    pair => pair.Key.Trim(),
                    pair => pair.Value?.Trim(),
                    StringComparer.OrdinalIgnoreCase);

            List<DataFilePreviewColumnRequest> requestedColumns = (request.Columns ?? new())
                .Where(column => !string.IsNullOrWhiteSpace(column.PropertyName))
                .GroupBy(column => column.PropertyName.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            var selectedColumns = requestedColumns
                .Select(column => column.PropertyName.Trim())
                .Where(metadataByName.ContainsKey)
                .ToList();

            if (selectedColumns.Count == 0)
            {
                return new DataFilePreviewResult();
            }

            var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

            string escapedDatabase = EscapeSqlIdentifier(sourceDatabase);
            string escapedTable = EscapeSqlIdentifier(sourceTable);

            await using var cmd = conn.CreateCommand();

            string selectList = string.Join(", ", selectedColumns.Select(column => $"[{EscapeSqlIdentifier(column)}]"));
            var whereClauses = new List<string>();
            int parameterIndex = 0;

            foreach (DataFilePreviewColumnRequest requestedColumn in requestedColumns)
            {
                string columnName = requestedColumn.PropertyName.Trim();
                if (!metadataByName.TryGetValue(columnName, out SourceColumnMetadata? metadataColumn))
                {
                    continue;
                }

                if (IsClientCodeMapping(requestedColumn.MappingParameter))
                {
                    string clientCode = (request.ClientCode ?? string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(clientCode))
                    {
                        string clientCodeParameterName = $"@f{parameterIndex++}";
                        whereClauses.Add($"CAST([{EscapeSqlIdentifier(columnName)}] AS nvarchar(4000)) LIKE {clientCodeParameterName}");
                        AddParameter(cmd, clientCodeParameterName, $"%{clientCode}%");
                    }

                    continue;
                }

                string parameterKey = (requestedColumn.MappingParameter ?? string.Empty).Trim();
                string filterValue = string.Empty;

                if (hasExplicitParameters && !string.IsNullOrWhiteSpace(parameterKey) && parameters.TryGetValue(parameterKey, out string? parameterFilterValue))
                {
                    filterValue = (parameterFilterValue ?? string.Empty).Trim();
                }

                if (hasExplicitParameters && string.IsNullOrWhiteSpace(parameterKey) && parameters.TryGetValue(columnName, out string? parameterNewFilterValue))
                {
                    filterValue = (parameterNewFilterValue ?? string.Empty).Trim();
                }

                if (!hasExplicitParameters && string.IsNullOrWhiteSpace(filterValue) && !string.IsNullOrWhiteSpace(parameterKey))
                {
                    filterValue = parameterKey;
                }

                if (string.IsNullOrWhiteSpace(filterValue))
                {
                    continue;
                }

                string parameterName = $"@f{parameterIndex++}";
                bool isDateTimeType = IsDateTimeTypeName(metadataColumn.DataType);
                bool isBooleanType = IsBooleanTypeName(metadataColumn.DataType);
                bool isDecimalType = IsDecimalTypeName(metadataColumn.DataType);
                bool isIntegerType = IsIntegerTypeName(metadataColumn.DataType);

                if (isDateTimeType)
                {
                    if (!TryParseDateTimeRangeFilter(filterValue, out DateTime? startDateTime, out DateTime? endDateTime))
                    {
                        throw new ArgumentException($"Mapping value for column '{columnName}' is not a valid datetime.");
                    }

                    string escapedColumnName = EscapeSqlIdentifier(columnName);
                    if (startDateTime.HasValue)
                    {
                        whereClauses.Add($"[{escapedColumnName}] >= {parameterName}");
                        AddParameter(cmd, parameterName, startDateTime.Value);
                    }

                    if (endDateTime.HasValue)
                    {
                        string endParameterName = $"@f{parameterIndex++}";
                        whereClauses.Add($"[{escapedColumnName}] <= {endParameterName}");
                        AddParameter(cmd, endParameterName, endDateTime.Value);
                    }
                }
                else if (isBooleanType)
                {
                    if (filterValue.Equals("all", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!TryParseBooleanFilter(filterValue, out bool boolValue))
                    {
                        throw new ArgumentException($"Mapping value for column '{columnName}' is not a valid boolean.");
                    }

                    whereClauses.Add($"[{EscapeSqlIdentifier(columnName)}] = {parameterName}");
                    AddParameter(cmd, parameterName, boolValue);
                }
                else if (isDecimalType)
                {
                    if (!TryParseDecimalRangeFilter(filterValue, out decimal? fromValue, out decimal? toValue))
                    {
                        throw new ArgumentException($"Mapping value for column '{columnName}' is not a valid decimal.");
                    }

                    string escapedColumnName = EscapeSqlIdentifier(columnName);
                    if (fromValue.HasValue)
                    {
                        whereClauses.Add($"TRY_CONVERT(decimal(38, 10), [{escapedColumnName}]) >= {parameterName}");
                        AddParameter(cmd, parameterName, fromValue.Value);
                    }

                    if (toValue.HasValue)
                    {
                        string toParameterName = $"@f{parameterIndex++}";
                        whereClauses.Add($"TRY_CONVERT(decimal(38, 10), [{escapedColumnName}]) <= {toParameterName}");
                        AddParameter(cmd, toParameterName, toValue.Value);
                    }
                }
                else if (isIntegerType)
                {
                    if (!TryParseIntegerFilter(filterValue, out long intValue))
                    {
                        throw new ArgumentException($"Mapping value for column '{columnName}' is not a valid number.");
                    }

                    whereClauses.Add($"TRY_CONVERT(bigint, [{EscapeSqlIdentifier(columnName)}]) = {parameterName}");
                    AddParameter(cmd, parameterName, intValue);
                }
                else
                {
                    if (TryParseMultiSelectFilter(filterValue, out List<string> selectedValues))
                    {
                        whereClauses.Add(BuildMultiSelectStringClause(cmd, columnName, selectedValues, ref parameterIndex));
                    }
                    else
                    {
                        whereClauses.Add($"CAST([{EscapeSqlIdentifier(columnName)}] AS nvarchar(4000)) LIKE {parameterName}");
                        AddParameter(cmd, parameterName, $"%{filterValue}%");
                    }
                }
            }

            string orderBySql = string.Empty;

            if (!string.IsNullOrEmpty(filterClientCodeColumn))
            {
                string parameterName = $"@f{parameterIndex++}";

                whereClauses.Add($"CAST([{EscapeSqlIdentifier(filterClientCodeColumn)}] AS nvarchar(4000)) LIKE {parameterName}");
                AddParameter(cmd, parameterName, $"%{request.ClientCode}%");
            }

            if (!string.IsNullOrEmpty(filterDateColumn))
            {
                string parameterName = $"@f{parameterIndex++}";

                string escapedColumnName = EscapeSqlIdentifier(filterDateColumn);
                if (dateFrom.HasValue)
                {
                    whereClauses.Add($"[{escapedColumnName}] >= {parameterName}");
                    AddParameter(cmd, parameterName, dateFrom);
                }

                if (dateTo.HasValue)
                {
                    string endParameterName = $"@f{parameterIndex++}";
                    whereClauses.Add($"[{escapedColumnName}] <= {endParameterName}");
                    AddParameter(cmd, endParameterName, dateTo);

                }
                orderBySql = $" ORDER BY {escapedColumnName}";
            }

            if (!string.IsNullOrEmpty(clintCodesForSql))
            {
                whereClauses.Add($"TR_ID in {clintCodesForSql}");
            }

            string whereSql = whereClauses.Count > 0
                ? " WHERE " + string.Join(" AND ", whereClauses)
                : string.Empty;

            string fromSql = $"FROM [{escapedDatabase}]..[{escapedTable}] WITH(NOLOCK)";

            cmd.CommandText =
                "SELECT COUNT(1) " +
                fromSql +
                whereSql;

            var result = new DataFilePreviewResult
            {
                Columns = selectedColumns
            };

            object? totalCountObj = await cmd.ExecuteScalarAsync();
            result.TotalRowCount = totalCountObj is null || totalCountObj == DBNull.Value
                ? 0
                : Convert.ToInt32(totalCountObj, CultureInfo.InvariantCulture);

            if (!request.IsExport && take > 0)
            {
                int offset = (page - 1) * take;
                string effectiveOrderBySql = string.IsNullOrWhiteSpace(orderBySql)
                    ? $" ORDER BY [{EscapeSqlIdentifier(selectedColumns[0])}]"
                    : orderBySql;

                cmd.CommandText =
                    $"SELECT {selectList} " +
                    fromSql +
                    whereSql +
                    effectiveOrderBySql +
                    $" OFFSET {offset} ROWS FETCH NEXT {take} ROWS ONLY";
            }
            else
            {
                string topSql = take > 0 ? $"TOP ({take}) " : string.Empty;

                cmd.CommandText =
                    $"SELECT {topSql}{selectList} " +
                    fromSql +
                    whereSql +
                    orderBySql;
            }

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                result.Rows.Add(row);
            }

            return result;
        }

        public async Task<DataFilePreviewResult> GetPreviewDataAsync(
            DataFilePreviewRequest request,
            DateTime? dateFrom,
            DateTime? dateTo)
        {
            if (request is null)
            {
                throw new ArgumentException("Preview request is required.");
            }

            var clintCodes = await GetTRIDsByUserIdAsync(request?.UserId);
            var clintCodesForSql = $"({string.Join(",", clintCodes.Select(code => $"'{code}'"))})";

            string sourceDatabase = (request.SourceDatabase ?? string.Empty).Trim();
            string sourceTable = (request.SourceTable ?? string.Empty).Trim();
            string sourceSP = (request.SourceSP ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(sourceDatabase))
            {
                throw new ArgumentException("Source database is required for preview.");
            }

            if (!string.IsNullOrWhiteSpace(sourceSP))
            {
                throw new InvalidOperationException("Preview currently supports Source Table only.");
            }

            if (string.IsNullOrWhiteSpace(sourceTable))
            {
                throw new ArgumentException("Source table is required for preview.");
            }

            int take = request.Take;
            int page = request.Page <= 0 ? 1 : request.Page;
            if (request.IsExport)
            {
                take = _scheduledJob.ExportSplit.MaxTotalRecord;
            }
            else
            {
                if (take <= 0)
                {
                    take = 0;
                }
                else if (take != 10 && take != 25 && take != 50 && take != 100)
                {
                    take = 10;
                }
            }

            var metadata = await GetSourceColumnMetadataAsync(sourceDatabase, sourceTable, null);
            if (metadata.Count == 0)
            {
                return new DataFilePreviewResult();
            }

            var metadataByName = metadata
                .Where(column => !string.IsNullOrWhiteSpace(column.Name))
                .ToDictionary(column => column.Name, StringComparer.OrdinalIgnoreCase);
            //bool hasExplicitParameters = request.Parameters is not null;
            bool hasExplicitParameters = request.Parameters is not null && request.Parameters.Count() > 0;
            var parameters = (request.Parameters ?? new Dictionary<string, string?>())
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
                .ToDictionary(
                    pair => pair.Key.Trim(),
                    pair => pair.Value?.Trim(),
                    StringComparer.OrdinalIgnoreCase);

            List<DataFilePreviewColumnRequest> requestedColumns = (request.Columns ?? new())
                .Where(column => !string.IsNullOrWhiteSpace(column.PropertyName))
                .GroupBy(column => column.PropertyName.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            var selectedColumns = requestedColumns
                .Select(column => column.PropertyName.Trim())
                .Where(metadataByName.ContainsKey)
                .ToList();

            if (selectedColumns.Count == 0)
            {
                return new DataFilePreviewResult();
            }

            var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

            string escapedDatabase = EscapeSqlIdentifier(sourceDatabase);
            string escapedTable = EscapeSqlIdentifier(sourceTable);

            await using var cmd = conn.CreateCommand();

            string selectList = string.Join(", ", selectedColumns.Select(column => $"[{EscapeSqlIdentifier(column)}]"));
            var whereClauses = new List<string>();
            int parameterIndex = 0;

            foreach (DataFilePreviewColumnRequest requestedColumn in requestedColumns)
            {
                string columnName = requestedColumn.PropertyName.Trim();
                if (!metadataByName.TryGetValue(columnName, out SourceColumnMetadata? metadataColumn))
                {
                    continue;
                }

                if (IsClientCodeMapping(requestedColumn.MappingParameter))
                {
                    string clientCode = (request.ClientCode ?? string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(clientCode))
                    {
                        string clientCodeParameterName = $"@f{parameterIndex++}";
                        whereClauses.Add($"CAST([{EscapeSqlIdentifier(columnName)}] AS nvarchar(4000)) LIKE {clientCodeParameterName}");
                        AddParameter(cmd, clientCodeParameterName, $"%{clientCode}%");
                    }

                    continue;
                }

                string parameterKey = (requestedColumn.MappingParameter ?? string.Empty).Trim();
                string filterValue = string.Empty;

                if (hasExplicitParameters && !string.IsNullOrWhiteSpace(parameterKey) && parameters.TryGetValue(parameterKey, out string? parameterFilterValue))
                {
                    filterValue = (parameterFilterValue ?? string.Empty).Trim();
                }

                if (hasExplicitParameters && string.IsNullOrWhiteSpace(parameterKey) && parameters.TryGetValue(columnName, out string? parameterNewFilterValue))
                {
                    filterValue = (parameterNewFilterValue ?? string.Empty).Trim();
                }

                if (!hasExplicitParameters && string.IsNullOrWhiteSpace(filterValue) && !string.IsNullOrWhiteSpace(parameterKey))
                {
                    filterValue = parameterKey;
                }

                if (string.IsNullOrWhiteSpace(filterValue))
                {
                    continue;
                }

                string parameterName = $"@f{parameterIndex++}";
                bool isDateTimeType = IsDateTimeTypeName(metadataColumn.DataType);
                bool isBooleanType = IsBooleanTypeName(metadataColumn.DataType);
                bool isDecimalType = IsDecimalTypeName(metadataColumn.DataType);
                bool isIntegerType = IsIntegerTypeName(metadataColumn.DataType);

                if (isDateTimeType)
                {
                    if (!TryParseDateTimeRangeFilter(filterValue, out DateTime? startDateTime, out DateTime? endDateTime))
                    {
                        throw new ArgumentException($"Mapping value for column '{columnName}' is not a valid datetime.");
                    }

                    string escapedColumnName = EscapeSqlIdentifier(columnName);
                    if (startDateTime.HasValue)
                    {
                        whereClauses.Add($"[{escapedColumnName}] >= {parameterName}");
                        AddParameter(cmd, parameterName, startDateTime.Value);
                    }

                    if (endDateTime.HasValue)
                    {
                        string endParameterName = $"@f{parameterIndex++}";
                        whereClauses.Add($"[{escapedColumnName}] <= {endParameterName}");
                        AddParameter(cmd, endParameterName, endDateTime.Value);
                    }
                }
                else if (isBooleanType)
                {
                    if (filterValue.Equals("all", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!TryParseBooleanFilter(filterValue, out bool boolValue))
                    {
                        throw new ArgumentException($"Mapping value for column '{columnName}' is not a valid boolean.");
                    }

                    whereClauses.Add($"[{EscapeSqlIdentifier(columnName)}] = {parameterName}");
                    AddParameter(cmd, parameterName, boolValue);
                }
                else if (isDecimalType)
                {
                    if (!TryParseDecimalRangeFilter(filterValue, out decimal? fromValue, out decimal? toValue))
                    {
                        throw new ArgumentException($"Mapping value for column '{columnName}' is not a valid decimal.");
                    }

                    string escapedColumnName = EscapeSqlIdentifier(columnName);
                    if (fromValue.HasValue)
                    {
                        whereClauses.Add($"TRY_CONVERT(decimal(38, 10), [{escapedColumnName}]) >= {parameterName}");
                        AddParameter(cmd, parameterName, fromValue.Value);
                    }

                    if (toValue.HasValue)
                    {
                        string toParameterName = $"@f{parameterIndex++}";
                        whereClauses.Add($"TRY_CONVERT(decimal(38, 10), [{escapedColumnName}]) <= {toParameterName}");
                        AddParameter(cmd, toParameterName, toValue.Value);
                    }
                }
                else if (isIntegerType)
                {
                    if (!TryParseIntegerFilter(filterValue, out long intValue))
                    {
                        throw new ArgumentException($"Mapping value for column '{columnName}' is not a valid number.");
                    }

                    whereClauses.Add($"TRY_CONVERT(bigint, [{EscapeSqlIdentifier(columnName)}]) = {parameterName}");
                    AddParameter(cmd, parameterName, intValue);
                }
                else
                {
                    if (TryParseMultiSelectFilter(filterValue, out List<string> selectedValues))
                    {
                        whereClauses.Add(BuildMultiSelectStringClause(cmd, columnName, selectedValues, ref parameterIndex));
                    }
                    else
                    {
                        whereClauses.Add($"CAST([{EscapeSqlIdentifier(columnName)}] AS nvarchar(4000)) LIKE {parameterName}");
                        AddParameter(cmd, parameterName, $"%{filterValue}%");
                    }
                }
            }

            string orderBySql = string.Empty;

            if (!string.IsNullOrEmpty(clintCodesForSql))
            {
                whereClauses.Add($"TR_ID in {clintCodesForSql}");
            }

            string whereSql = whereClauses.Count > 0
                ? " WHERE " + string.Join(" AND ", whereClauses)
                : string.Empty;

            string fromSql = $"FROM [{escapedDatabase}]..[{escapedTable}] WITH(NOLOCK)";

            cmd.CommandText =
                "SELECT COUNT(1) " +
                fromSql +
                whereSql;

            var result = new DataFilePreviewResult
            {
                Columns = selectedColumns
            };

            object? totalCountObj = await cmd.ExecuteScalarAsync();
            result.TotalRowCount = totalCountObj is null || totalCountObj == DBNull.Value
                ? 0
                : Convert.ToInt32(totalCountObj, CultureInfo.InvariantCulture);

            if (!request.IsExport && take > 0)
            {
                int offset = (page - 1) * take;
                string effectiveOrderBySql = string.IsNullOrWhiteSpace(orderBySql)
                    ? $" ORDER BY [{EscapeSqlIdentifier(selectedColumns[0])}]"
                    : orderBySql;

                cmd.CommandText =
                    $"SELECT {selectList} " +
                    fromSql +
                    whereSql +
                    effectiveOrderBySql +
                    $" OFFSET {offset} ROWS FETCH NEXT {take} ROWS ONLY";
            }
            else
            {
                string topSql = take > 0 ? $"TOP ({take}) " : string.Empty;

                cmd.CommandText =
                    $"SELECT {topSql}{selectList} " +
                    fromSql +
                    whereSql +
                    orderBySql;
            }

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                result.Rows.Add(row);
            }

            return result;
        }

        public async Task<DataFilePreviewResult> GetPreviewDataAsync(DataFilePreviewRequest request)
        {
            if (request is null)
            {
                throw new ArgumentException("Preview request is required.");
            }

            var clintCodes = await GetTRIDsByUserIdAsync(request?.UserId);
            var clintCodesForSql = $"({string.Join(",", clintCodes.Select(code => $"'{code}'"))})";

            string sourceDatabase = (request.SourceDatabase ?? string.Empty).Trim();
            string sourceTable = (request.SourceTable ?? string.Empty).Trim();
            string sourceSP = (request.SourceSP ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(sourceDatabase))
            {
                throw new ArgumentException("Source database is required for preview.");
            }

            if (!string.IsNullOrWhiteSpace(sourceSP))
            {
                throw new InvalidOperationException("Preview currently supports Source Table only.");
            }

            if (string.IsNullOrWhiteSpace(sourceTable))
            {
                throw new ArgumentException("Source table is required for preview.");
            }

            int take = request.Take;
            int page = request.Page <= 0 ? 1 : request.Page;
            if (request.IsExport)
            {
                take = _scheduledJob.ExportSplit.MaxTotalRecord;
            }
            else
            {
                if (take <= 0)
                {
                    take = 0;
                }
                else if (take != 10 && take != 25 && take != 50 && take != 100)
                {
                    take = 10;
                }
            }

            var metadata = await GetSourceColumnMetadataAsync(sourceDatabase, sourceTable, null);
            if (metadata.Count == 0)
            {
                return new DataFilePreviewResult();
            }

            var metadataByName = metadata
                .Where(column => !string.IsNullOrWhiteSpace(column.Name))
                .ToDictionary(column => column.Name, StringComparer.OrdinalIgnoreCase);

            List<DataFilePreviewColumnRequest> requestedColumns = (request.Columns ?? new())
                .Where(column => !string.IsNullOrWhiteSpace(column.PropertyName))
                .GroupBy(column => column.PropertyName.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            var selectedColumns = requestedColumns
                .Select(column => column.PropertyName.Trim())
                .Where(metadataByName.ContainsKey)
                .ToList();

            if (selectedColumns.Count == 0)
            {
                return new DataFilePreviewResult();
            }

            var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

            string escapedDatabase = EscapeSqlIdentifier(sourceDatabase);
            string escapedTable = EscapeSqlIdentifier(sourceTable);

            await using var cmd = conn.CreateCommand();

            string selectList = string.Join(", ", selectedColumns.Select(column => $"[{EscapeSqlIdentifier(column)}]"));
            var whereClauses = new List<string>();
            int parameterIndex = 0;

            foreach (DataFilePreviewColumnRequest requestedColumn in requestedColumns)
            {
                string columnName = requestedColumn.PropertyName.Trim();
                if (!metadataByName.TryGetValue(columnName, out SourceColumnMetadata? metadataColumn))
                {
                    continue;
                }

                if (IsClientCodeMapping(requestedColumn.MappingParameter))
                {
                    string clientCode = (request.ClientCode ?? string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(clientCode))
                    {
                        string clientCodeParameterName = $"@f{parameterIndex++}";
                        whereClauses.Add($"CAST([{EscapeSqlIdentifier(columnName)}] AS nvarchar(4000)) LIKE {clientCodeParameterName}");
                        AddParameter(cmd, clientCodeParameterName, $"%{clientCode}%");
                    }

                    continue;
                }

                string filterValue = (requestedColumn.MappingParameter ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(filterValue))
                {
                    continue;
                }

                string parameterName = $"@f{parameterIndex++}";
                bool isDateTimeType = IsDateTimeTypeName(metadataColumn.DataType);
                bool isBooleanType = IsBooleanTypeName(metadataColumn.DataType);
                bool isDecimalType = IsDecimalTypeName(metadataColumn.DataType);
                bool isIntegerType = IsIntegerTypeName(metadataColumn.DataType);

                if (isDateTimeType)
                {
                    if (!TryParseDateTimeRangeFilter(filterValue, out DateTime? startDateTime, out DateTime? endDateTime))
                    {
                        throw new ArgumentException($"Mapping value for column '{columnName}' is not a valid datetime.");
                    }

                    string escapedColumnName = EscapeSqlIdentifier(columnName);
                    if (startDateTime.HasValue)
                    {
                        whereClauses.Add($"[{escapedColumnName}] >= {parameterName}");
                        AddParameter(cmd, parameterName, startDateTime.Value);
                    }

                    if (endDateTime.HasValue)
                    {
                        string endParameterName = $"@f{parameterIndex++}";
                        whereClauses.Add($"[{escapedColumnName}] <= {endParameterName}");
                        AddParameter(cmd, endParameterName, endDateTime.Value);
                    }
                }
                else if (isBooleanType)
                {
                    if (filterValue.Equals("all", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!TryParseBooleanFilter(filterValue, out bool boolValue))
                    {
                        throw new ArgumentException($"Mapping value for column '{columnName}' is not a valid boolean.");
                    }

                    whereClauses.Add($"[{EscapeSqlIdentifier(columnName)}] = {parameterName}");
                    AddParameter(cmd, parameterName, boolValue);
                }
                else if (isDecimalType)
                {
                    if (!TryParseDecimalRangeFilter(filterValue, out decimal? fromValue, out decimal? toValue))
                    {
                        throw new ArgumentException($"Mapping value for column '{columnName}' is not a valid decimal.");
                    }

                    string escapedColumnName = EscapeSqlIdentifier(columnName);
                    if (fromValue.HasValue)
                    {
                        whereClauses.Add($"TRY_CONVERT(decimal(38, 10), [{escapedColumnName}]) >= {parameterName}");
                        AddParameter(cmd, parameterName, fromValue.Value);
                    }

                    if (toValue.HasValue)
                    {
                        string toParameterName = $"@f{parameterIndex++}";
                        whereClauses.Add($"TRY_CONVERT(decimal(38, 10), [{escapedColumnName}]) <= {toParameterName}");
                        AddParameter(cmd, toParameterName, toValue.Value);
                    }
                }
                else if (isIntegerType)
                {
                    if (!TryParseIntegerFilter(filterValue, out long intValue))
                    {
                        throw new ArgumentException($"Mapping value for column '{columnName}' is not a valid number.");
                    }

                    whereClauses.Add($"TRY_CONVERT(bigint, [{EscapeSqlIdentifier(columnName)}]) = {parameterName}");
                    AddParameter(cmd, parameterName, intValue);
                }
                else
                {
                    if (TryParseMultiSelectFilter(filterValue, out List<string> selectedValues))
                    {
                        whereClauses.Add(BuildMultiSelectStringClause(cmd, columnName, selectedValues, ref parameterIndex));
                    }
                    else
                    {
                        whereClauses.Add($"CAST([{EscapeSqlIdentifier(columnName)}] AS nvarchar(4000)) LIKE {parameterName}");
                        AddParameter(cmd, parameterName, $"%{filterValue}%");
                    }
                }
            }

            if (!string.IsNullOrEmpty(clintCodesForSql))
            {
                whereClauses.Add($"TR_ID in {clintCodesForSql}");
            }

            string whereSql = whereClauses.Count > 0
                ? " WHERE " + string.Join(" AND ", whereClauses)
                : string.Empty;

            string fromSql = $"FROM [{escapedDatabase}]..[{escapedTable}]";

            cmd.CommandText =
                "SELECT COUNT(1) " +
                fromSql +
                whereSql;

            var result = new DataFilePreviewResult
            {
                Columns = selectedColumns
            };

            object? totalCountObj = await cmd.ExecuteScalarAsync();
            result.TotalRowCount = totalCountObj is null || totalCountObj == DBNull.Value
                ? 0
                : Convert.ToInt32(totalCountObj, CultureInfo.InvariantCulture);

            if (!request.IsExport && take > 0)
            {
                int offset = (page - 1) * take;
                cmd.CommandText =
                    $"SELECT {selectList} " +
                    fromSql +
                    whereSql +
                    $" ORDER BY [{EscapeSqlIdentifier(selectedColumns[0])}] OFFSET {offset} ROWS FETCH NEXT {take} ROWS ONLY";
            }
            else
            {
                string topSql = take > 0 ? $"TOP ({take}) " : string.Empty;

                cmd.CommandText =
                    $"SELECT {topSql}{selectList} " +
                    fromSql +
                    whereSql;
            }

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                result.Rows.Add(row);
            }

            return result;
        }

        public async Task<List<SourceColumnMetadata>> GetSourceColumnMetadataAsync(string? sourceDatabase, string? sourceTable, string? sourceSP)
        {
            if (string.IsNullOrWhiteSpace(sourceDatabase))
            {
                return new();
            }

            var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

            var safeDatabase = sourceDatabase.Trim();

            if (!string.IsNullOrWhiteSpace(sourceTable))
            {
                return await GetSourceColumnsFromTableOrViewAsync(conn, safeDatabase, sourceTable.Trim());
            }

            if (!string.IsNullOrWhiteSpace(sourceSP))
            {
                return await GetSourceColumnsFromStoredProcedureAsync(conn, safeDatabase, sourceSP.Trim());
            }

            return new();
        }

        public async Task<List<string>> GetSourceParametersAsync(string? sourceDatabase, string? sourceTable, string? sourceSP)
        {
            if (string.IsNullOrWhiteSpace(sourceDatabase) || string.IsNullOrWhiteSpace(sourceSP))
            {
                return new();
            }

            var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

            var escapedDatabase = EscapeSqlIdentifier(sourceDatabase.Trim());
            var items = new List<string>();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT p.name " +
                "FROM [" + escapedDatabase + "].sys.parameters p " +
                "INNER JOIN [" + escapedDatabase + "].sys.procedures sp ON p.object_id = sp.object_id " +
                "WHERE sp.name = @spName AND p.parameter_id > 0 AND p.is_output = 0 " +
                "ORDER BY p.parameter_id";

            var p = cmd.CreateParameter();
            p.ParameterName = "@spName";
            p.Value = sourceSP.Trim();
            cmd.Parameters.Add(p);

            try
            {
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(0))
                    {
                        items.Add(reader.GetString(0));
                    }
                }
            }
            catch (SqlException ex) when (ex.Number is 916 or 229 or 911)
            {
                return new();
            }

            return items;
        }

        public async Task<List<string>> GetDistinctColumnValuesAsync(
            string? userId,
            string? sourceDatabase,
            string? sourceTable,
            string? sourceSP,
            string? columnName,
            string? search = null,
            int take = 50)
        {
            if (string.IsNullOrWhiteSpace(sourceDatabase) ||
                string.IsNullOrWhiteSpace(sourceTable) ||
                string.IsNullOrWhiteSpace(columnName) ||
                !string.IsNullOrWhiteSpace(sourceSP))
            {
                return new();
            }

            int safeTake = Math.Clamp(take, 1, 100);
            //string normalizedDatabase = sourceDatabase.Trim();
            string normalizedDatabase = _context.Database.GetDbConnection().Database;
            string normalizedTable = sourceTable.Trim();
            string normalizedColumn = columnName.Trim();
            string normalizedSearch = (search ?? string.Empty).Trim();

            List<SourceColumnMetadata> metadata = await GetSourceColumnMetadataAsync(normalizedDatabase, normalizedTable, null);
            bool columnExists = metadata.Any(column =>
                !string.IsNullOrWhiteSpace(column.Name) &&
                column.Name.Equals(normalizedColumn, StringComparison.OrdinalIgnoreCase));

            if (!columnExists)
            {
                return new();
            }

            var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

            string escapedDatabase = EscapeSqlIdentifier(normalizedDatabase);
            string escapedTable = EscapeSqlIdentifier(normalizedTable);
            string escapedColumn = EscapeSqlIdentifier(normalizedColumn);

            var trList = await GetTRIDsByUserIdAsync(userId);
            var trListForSql = $"({string.Join(",", trList.Select(code => $"'{code}'"))})";

            await using var cmd = conn.CreateCommand();

            string whereSearchSql = string.IsNullOrWhiteSpace(normalizedSearch)
                ? string.Empty
                : " AND CAST([" + escapedColumn + "] AS nvarchar(4000)) LIKE @search";

            string whereTRClauses = string.Empty;
            if (!string.IsNullOrEmpty(trListForSql))
            {
                whereTRClauses = $" AND TR_ID in {trListForSql}";
            }

            cmd.CommandText =
                "SELECT DISTINCT TOP (" + safeTake + ") CAST([" + escapedColumn + "] AS nvarchar(4000)) AS [Value] " +
                "FROM [" + escapedDatabase + "]..[" + escapedTable + "] WITH(NOLOCK) " +
                "WHERE [" + escapedColumn + "] IS NOT NULL" +
                whereSearchSql +
                whereTRClauses +
                " ORDER BY [Value]";

            if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                AddParameter(cmd, "@search", $"%{normalizedSearch}%");
            }

            List<string> values = new();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (reader.IsDBNull(0))
                {
                    continue;
                }

                string value = reader.GetString(0).Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    values.Add(value);
                }
            }

            return values
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static async Task<List<SourceColumnMetadata>> GetSourceColumnsFromTableOrViewAsync(
            System.Data.Common.DbConnection conn,
            string safeDatabase,
            string sourceTable)
        {
            var escapedDatabase = EscapeSqlIdentifier(safeDatabase);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT COLUMN_NAME, DATA_TYPE " +
                "FROM [" + escapedDatabase + "].INFORMATION_SCHEMA.COLUMNS " +
                "WHERE TABLE_NAME = @tableName " +
                "ORDER BY ORDINAL_POSITION";

            var p = cmd.CreateParameter();
            p.ParameterName = "@tableName";
            p.Value = sourceTable;
            cmd.Parameters.Add(p);

            var items = new List<SourceColumnMetadata>();
            try
            {
                await using var reader = await cmd.ExecuteReaderAsync();
                int nameOrdinal = TryGetOrdinal(reader, "COLUMN_NAME");
                int dataTypeOrdinal = TryGetOrdinal(reader, "DATA_TYPE");

                while (await reader.ReadAsync())
                {
                    string? name = nameOrdinal < 0 || reader.IsDBNull(nameOrdinal)
                        ? null
                        : reader.GetString(nameOrdinal);

                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        string? dataType = dataTypeOrdinal < 0 || reader.IsDBNull(dataTypeOrdinal)
                            ? null
                            : reader.GetString(dataTypeOrdinal);

                        items.Add(new SourceColumnMetadata
                        {
                            Name = name,
                            DataType = dataType
                        });
                    }
                }
            }
            catch (SqlException ex) when (ex.Number is 916 or 229 or 911)
            {
                return new();
            }

            return items;
        }

        private static async Task<List<SourceColumnMetadata>> GetSourceColumnsFromStoredProcedureAsync(
            System.Data.Common.DbConnection conn,
            string safeDatabase,
            string sourceSP)
        {
            var escapedDatabase = EscapeSqlIdentifier(safeDatabase);
            var escapedProcedure = EscapeSqlIdentifier(sourceSP);

            await using (var validateCmd = conn.CreateCommand())
            {
                validateCmd.CommandText =
                    "SELECT name FROM [" + escapedDatabase + "].sys.procedures " +
                    "WHERE is_ms_shipped = 0 AND name = @spName";

                var p = validateCmd.CreateParameter();
                p.ParameterName = "@spName";
                p.Value = sourceSP;
                validateCmd.Parameters.Add(p);

                var exists = await validateCmd.ExecuteScalarAsync() as string;
                if (string.IsNullOrWhiteSpace(exists))
                {
                    return new();
                }
            }

            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "DECLARE @tsql nvarchar(max) = N'EXEC [" + escapedProcedure + "]'; " +
                "EXEC [" + escapedDatabase + "].sys.sp_describe_first_result_set " +
                "@tsql = @tsql, @params = NULL, @browse_information_mode = 0;";

            var items = new List<SourceColumnMetadata>();
            try
            {
                await using var reader = await cmd.ExecuteReaderAsync();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                int nameOrdinal = TryGetOrdinal(reader, "name");
                int dataTypeOrdinal = TryGetOrdinal(reader, "system_type_name");
                int hiddenOrdinal = TryGetOrdinal(reader, "is_hidden");
                int errorOrdinal = TryGetOrdinal(reader, "error_number");

                while (await reader.ReadAsync())
                {
                    string? name = nameOrdinal < 0 || reader.IsDBNull(nameOrdinal)
                        ? null
                        : reader.GetString(nameOrdinal);

                    bool isHidden = hiddenOrdinal >= 0 &&
                                    !reader.IsDBNull(hiddenOrdinal) &&
                                    reader.GetBoolean(hiddenOrdinal);

                    bool hasError = errorOrdinal >= 0 && !reader.IsDBNull(errorOrdinal);

                    if (isHidden || hasError || string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    if (seen.Add(name))
                    {
                        string? dataType = dataTypeOrdinal < 0 || reader.IsDBNull(dataTypeOrdinal)
                            ? null
                            : reader.GetString(dataTypeOrdinal);

                        items.Add(new SourceColumnMetadata
                        {
                            Name = name,
                            DataType = dataType
                        });
                    }
                }
            }
            catch (SqlException ex) when (ex.Number is 916 or 229 or 911 or 11514)
            {
                return new();
            }

            return items;
        }

        private static int TryGetOrdinal(System.Data.Common.DbDataReader reader, string columnName)
        {
            try
            {
                return reader.GetOrdinal(columnName);
            }
            catch (IndexOutOfRangeException)
            {
                return -1;
            }
        }

        private static bool IsDateTimeTypeName(string? dataType)
        {
            string normalized = (dataType ?? string.Empty).Trim().ToLowerInvariant();
            return normalized.Contains("date") ||
                normalized.Contains("time");
        }

        private static bool IsBooleanTypeName(string? dataType)
        {
            string normalized = (dataType ?? string.Empty).Trim().ToLowerInvariant();
            return normalized == "bit" || normalized.StartsWith("bit(");
        }

        private static bool IsClientCodeMapping(string? mappingParameter)
        {
            string normalized = (mappingParameter ?? string.Empty).Trim();
            return !string.IsNullOrWhiteSpace(normalized)
                && (normalized.Contains("ClientCode", StringComparison.OrdinalIgnoreCase)
                    || normalized.Contains("ClintCode", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsIntegerTypeName(string? dataType)
        {
            string normalized = (dataType ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalized) || IsBooleanTypeName(normalized))
            {
                return false;
            }

            return normalized.Contains("int");
        }

        private static bool IsDecimalTypeName(string? dataType)
        {
            string normalized = (dataType ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalized) || IsBooleanTypeName(normalized))
            {
                return false;
            }

            return normalized.Contains("decimal") ||
                normalized.Contains("numeric") ||
                normalized.Contains("float") ||
                normalized.Contains("real") ||
                normalized.Contains("money");
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
                    // Fall through to delimited parsing.
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

        private static string BuildMultiSelectStringClause(
            DbCommand cmd,
            string columnName,
            IEnumerable<string> selectedValues,
            ref int parameterIndex)
        {
            List<string> predicates = new();
            string escapedColumnName = EscapeSqlIdentifier(columnName);

            foreach (string selectedValue in selectedValues)
            {
                string parameterName = $"@f{parameterIndex++}";
                predicates.Add($"CAST([{escapedColumnName}] AS nvarchar(4000)) = {parameterName}");
                AddParameter(cmd, parameterName, selectedValue);
            }

            return "(" + string.Join(" OR ", predicates) + ")";
        }

        private static void AddParameter(System.Data.Common.DbCommand cmd, string name, object? value)
        {
            var parameter = cmd.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(parameter);
        }

        private static string EscapeSqlIdentifier(string value)
            => value.Replace("]", "]]", StringComparison.Ordinal);

        public async Task<List<DataFileColumn>> SyncDataFileColumnsAsync(int dataFileDefinitionId, IReadOnlyList<SourceColumnMetadata> sourceColumns)
        {
            if (dataFileDefinitionId <= 0)
            {
                return new();
            }

            DataFileDefinition? dataFileDefinition = await _context.DataFileDefinitions
                .Include(definition => definition.Columns)
                .FirstOrDefaultAsync(definition => definition.Id == dataFileDefinitionId);

            if (dataFileDefinition is null)
            {
                return new();
            }

            if (sourceColumns is null || sourceColumns.Count == 0)
            {
                return dataFileDefinition.Columns
                    .OrderBy(column => column.DisplayOrder)
                    .ThenBy(column => column.Id)
                    .ToList();
            }

            List<DataFileColumn> existingColumns = dataFileDefinition.Columns
                .OrderBy(column => column.DisplayOrder)
                .ThenBy(column => column.Id)
                .ToList();

            Dictionary<string, DataFileColumn> existingColumnsByName = existingColumns
                .Where(column => !string.IsNullOrWhiteSpace(column.PropertyName))
                .GroupBy(column => column.PropertyName.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            HashSet<string> liveColumnNames = new(StringComparer.OrdinalIgnoreCase);
            bool hasChanges = false;
            int nextDisplayOrder = existingColumns.Count == 0 ? 0 : existingColumns.Max(column => column.DisplayOrder);

            foreach (SourceColumnMetadata sourceColumn in sourceColumns)
            {
                string propertyName = (sourceColumn.Name ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(propertyName) || !liveColumnNames.Add(propertyName))
                {
                    continue;
                }

                if (existingColumnsByName.TryGetValue(propertyName, out DataFileColumn? existingColumn))
                {
                    string normalizedDataType = (sourceColumn.DataType ?? string.Empty).Trim();

                    if (!existingColumn.PropertyName.Equals(propertyName, StringComparison.Ordinal) ||
                        !string.Equals(existingColumn.PropertyType, normalizedDataType, StringComparison.Ordinal))
                    {
                        hasChanges = true;
                    }

                    existingColumn.PropertyName = propertyName;
                    existingColumn.PropertyType = string.IsNullOrWhiteSpace(normalizedDataType)
                        ? existingColumn.PropertyType
                        : normalizedDataType;
                    continue;
                }

                dataFileDefinition.Columns.Add(new DataFileColumn
                {
                    DataFileDefinitionId = dataFileDefinitionId,
                    PropertyName = propertyName,
                    PropertyType = (sourceColumn.DataType ?? string.Empty).Trim(),
                    DefaultLabel = propertyName,
                    DisplayOrder = ++nextDisplayOrder
                });
                hasChanges = true;
            }

            List<DataFileColumn> removedColumns = existingColumns
                .Where(column => !liveColumnNames.Contains(column.PropertyName))
                .ToList();

            if (removedColumns.Count > 0)
            {
                _context.DataFileColumns.RemoveRange(removedColumns);
                hasChanges = true;
            }

            if (hasChanges)
            {
                dataFileDefinition.ModifiedAt = DateTimeHelper.GetCurrentLocalTime();
                await _context.SaveChangesAsync();
            }

            return new List<DataFileColumn>();

            //return await _context.DataFileColumns
            //    .AsNoTracking()
            //    .Where(column => column.DataFileDefinitionId == dataFileDefinitionId)
            //    .OrderBy(column => column.DisplayOrder)
            //    .ThenBy(column => column.Id)
            //    .ToListAsync();
        }

        public async Task<List<DataFileDefinition>> GetAllWithColumnsAsync()
            => await _context.DataFileDefinitions
                .AsNoTracking()
                .Include(dataFile => dataFile.Columns.OrderBy(column => column.DisplayOrder))
                .OrderBy(dataFile => dataFile.DataFileName)
                .ToListAsync();

        public async Task<List<DataFileDefinition>> GetAllWithColumnsAsync(string userId)
        {
            var datafiles = await _context.DataFileDefinitions
                .AsNoTracking()
                .Include(dataFile => dataFile.Columns.OrderBy(column => column.DisplayOrder))
                .OrderBy(dataFile => dataFile.DataFileName)
                .ToListAsync();

            return datafiles.Where(dataFile => dataFile.UserId == userId ).ToList();
        }

        public async Task<List<DataFileDefinition>> GetAllWithColumnsAsync(string userId, string userDepartment)
        {
            int? userDepartmentId = await ResolveUserDepartmentIdAsync(userDepartment);

            var datafiles = await _context.DataFileDefinitions
                .AsNoTracking()
                .Include(dataFile => dataFile.Columns.OrderBy(column => column.DisplayOrder))
                .OrderBy(dataFile => dataFile.DataFileName)
                .ToListAsync();

            return datafiles
                    .Where(dataFile => (string.IsNullOrEmpty(dataFile.Departments) && dataFile.UserId == userId) || IsVisibleToDepartment(dataFile.Departments, userDepartment, userDepartmentId))
                    .ToList();
        }

        public async Task<DataFileDefinition?> GetByIdWithColumnsAsync(int id)
            => await _context.DataFileDefinitions
                .Include(definition => definition.Columns.OrderBy(column => column.DisplayOrder))
                .FirstOrDefaultAsync(definition => definition.Id == id);

        public async Task<DataFileDefinition> CreateAsync(
            DataFileDefinition dataFileDefinition,
            IEnumerable<DataFileColumn> columns)
        {
            _context.DataFileDefinitions.Add(dataFileDefinition);
            await _context.SaveChangesAsync();

            foreach (DataFileColumn column in columns)
            {
                column.DataFileDefinitionId = dataFileDefinition.Id;
                _context.DataFileColumns.Add(column);
            }

            await _context.SaveChangesAsync();
            return dataFileDefinition;
        }

        public async Task UpdateAsync(
            DataFileDefinition dataFileDefinitionRequest,
            IEnumerable<DataFileColumn> columns,
            IEnumerable<int> deletedColumnIds)
        {
            DataFileDefinition? dataFileDefinition = await _context.DataFileDefinitions
                .FirstOrDefaultAsync(definition => definition.Id == dataFileDefinitionRequest.Id);

            if (dataFileDefinition is null)
            {
                return;
            }

            dataFileDefinition.DataFileName = dataFileDefinitionRequest.DataFileName;
            dataFileDefinition.SourceDatabase = dataFileDefinitionRequest.SourceDatabase;
            dataFileDefinition.SourceTable = dataFileDefinitionRequest.SourceTable;
            dataFileDefinition.SourceSP = dataFileDefinitionRequest.SourceSP;
            dataFileDefinition.Parameters = dataFileDefinitionRequest.Parameters;
            dataFileDefinition.IsActive = dataFileDefinitionRequest.IsActive;
            dataFileDefinition.Departments = dataFileDefinitionRequest.Departments;
            dataFileDefinition.FilterDateColumn = dataFileDefinitionRequest.FilterDateColumn;
            dataFileDefinition.FilterClientCodeColumn = dataFileDefinitionRequest.FilterClientCodeColumn;
            //dataFileDefinition.UserId = dataFileDefinitionRequest.UserId;
            dataFileDefinition.ModifiedBy = dataFileDefinitionRequest.ModifiedBy;
            dataFileDefinition.ModifiedAt = DateTimeHelper.GetCurrentLocalTime();

            foreach (int deletedColumnId in deletedColumnIds)
            {
                DataFileColumn? deletedColumn = await _context.DataFileColumns.FindAsync(deletedColumnId);
                if (deletedColumn is not null)
                {
                    _context.DataFileColumns.Remove(deletedColumn);
                }
            }

            foreach (DataFileColumn column in columns)
            {
                if (column.Id == 0)
                {
                    column.DataFileDefinitionId = dataFileDefinition.Id;
                    _context.DataFileColumns.Add(column);
                }
                else
                {
                    DataFileColumn? existingColumn = await _context.DataFileColumns.FindAsync(column.Id);
                    if (existingColumn is not null)
                    {
                        existingColumn.PropertyName = column.PropertyName;
                        existingColumn.DefaultLabel = column.DefaultLabel;
                        existingColumn.MappingParameter = column.MappingParameter;
                        existingColumn.MappingParameterFilter = column.MappingParameterFilter;
                        existingColumn.DisplayOrder = column.DisplayOrder;
                        existingColumn.IsDeleted = column.IsDeleted;
                    }
                }
            }

            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            DataFileDefinition? dataFileDefinition = await _context.DataFileDefinitions
                .Include(definition => definition.Columns)
                .FirstOrDefaultAsync(definition => definition.Id == id);

            if (dataFileDefinition is null)
            {
                return;
            }

            _context.DataFileColumns.RemoveRange(dataFileDefinition.Columns);
            _context.DataFileDefinitions.Remove(dataFileDefinition);
            await _context.SaveChangesAsync();
        }

        public async Task ToggleActiveAsync(int id)
        {
            DataFileDefinition? dataFileDefinition = await _context.DataFileDefinitions
                .FirstOrDefaultAsync(definition => definition.Id == id);

            if (dataFileDefinition is null)
            {
                return;
            }

            dataFileDefinition.IsActive = !dataFileDefinition.IsActive;
            dataFileDefinition.ModifiedAt = DateTimeHelper.GetCurrentLocalTime();
            await _context.SaveChangesAsync();
        }

        private async Task<int?> ResolveUserDepartmentIdAsync(string? userDepartment)
        {
            if (string.IsNullOrWhiteSpace(userDepartment))
            {
                return null;
            }

            string normalizedUserDepartment = userDepartment.Trim();

            return await _context.Departments
                .AsNoTracking()
                .Where(department => department.IsActive)
                .Where(department => department.Name == normalizedUserDepartment)
                .Select(department => (int?)department.Id)
                .FirstOrDefaultAsync();
        }

        private static bool IsVisibleToDepartment(string? reportDepartments, string? userDepartment, int? userDepartmentId)
        {
            if (string.IsNullOrWhiteSpace(reportDepartments))
            {
                //return true;
                return false;
            }

            if (string.IsNullOrWhiteSpace(userDepartment))
            {
                return false;
            }

            string normalizedUserDepartment = userDepartment.Trim();

            HashSet<string> configuredDepartments = reportDepartments
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (configuredDepartments.Contains(normalizedUserDepartment))
            {
                return true;
            }

            if (!userDepartmentId.HasValue)
            {
                return false;
            }

            string userDepartmentIdToken = userDepartmentId.Value.ToString();

            return configuredDepartments.Contains(userDepartmentIdToken);
        }

        private async Task<List<string>> GetTRIDsByUserIdAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return new List<string>();
            }

            string configuredSpName = (_clientCodeLookupOptions.GetTRsStoredProcedureName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(configuredSpName))
            {
                return new List<string>();
            }

            string configuredParameterName = NormalizeParameterName(_clientCodeLookupOptions.UserIdParameterName, "@UserId");
            string configuredResponseColumnName = (_clientCodeLookupOptions.ResponseColumnName ?? string.Empty).Trim();
            configuredResponseColumnName = string.IsNullOrWhiteSpace(configuredResponseColumnName)
                ? "ClientCode"
                : configuredResponseColumnName;

            var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

            string? safeSp = await ValidateSpNameAsync(conn, configuredSpName);
            if (string.IsNullOrWhiteSpace(safeSp))
            {
                return new List<string>();
            }

            HashSet<string> parameterNames = await GetStoredProcedureParameterNamesCoreAsync(conn, safeSp);
            Dictionary<string, object?> parameters = new(StringComparer.OrdinalIgnoreCase);
            string? userIdParameterName = ResolveUserIdParameterName(parameterNames, configuredParameterName);
            if (!string.IsNullOrWhiteSpace(userIdParameterName))
            {
                parameters[userIdParameterName] = userId.Trim();
            }

            List<Dictionary<string, object?>> rows = await ExecuteReaderAsync(
                conn,
                $"[{safeSp}]",
                CommandType.StoredProcedure,
                parameters);

            //return rows
            //    .Select(row => ExtractClientCodeValue(row, configuredResponseColumnName))
            //    .Where(value => !string.IsNullOrWhiteSpace(value))
            //    .Select(value => value!)
            //    .Distinct(StringComparer.OrdinalIgnoreCase)
            //    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            //    .ToList();

            return rows
                .Select(row => ExtractClientCodeValue(row, configuredResponseColumnName))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .SelectMany(value => value!.Split(',', StringSplitOptions.RemoveEmptyEntries)) // split here
                .Select(code => code.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string NormalizeParameterName(string? parameterName, string fallback)
        {
            string normalized = (parameterName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return fallback;
            }

            return normalized.StartsWith("@", StringComparison.Ordinal) ? normalized : "@" + normalized;
        }

        private static async Task<string?> ValidateSpNameAsync(DbConnection conn, string spName)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT name FROM sys.procedures WHERE name = @name";
            AddParam(cmd, "@name", spName);
            return await cmd.ExecuteScalarAsync() as string;
        }

        private static async Task<HashSet<string>> GetStoredProcedureParameterNamesCoreAsync(DbConnection conn, string spName)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT p.name " +
                "FROM sys.parameters p " +
                "INNER JOIN sys.procedures sp ON p.object_id = sp.object_id " +
                "WHERE sp.name = @name AND p.parameter_id > 0 AND p.is_output = 0";
            AddParam(cmd, "@name", spName);

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                names.Add(reader.GetString(0));

            return names;
        }

        private static void AddParam(System.Data.Common.DbCommand cmd, string name, object? value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }

        private static string? ResolveUserIdParameterName(
            IEnumerable<string> parameterNames,
            string configuredParameterName)
        {
            string? configuredMatch = parameterNames.FirstOrDefault(name =>
                name.Equals(configuredParameterName, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(configuredMatch))
            {
                return configuredMatch;
            }

            string[] preferredNames = ["@UserId", "@userId", "@userid", "@UserID", "@userID"];

            foreach (string preferredName in preferredNames)
            {
                string? match = parameterNames.FirstOrDefault(name =>
                    name.Equals(preferredName, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrWhiteSpace(match))
                {
                    return match;
                }
            }

            return parameterNames.FirstOrDefault();
        }

        private static async Task<List<Dictionary<string, object?>>> ExecuteReaderAsync(
            DbConnection conn,
            string commandText,
            CommandType commandType,
            IReadOnlyDictionary<string, object?>? parameters = null)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = commandText;
            cmd.CommandType = commandType;

            if (parameters is not null)
            {
                foreach (var parameter in parameters)
                    AddParam(cmd, parameter.Key, parameter.Value);
            }

            var rows = new List<Dictionary<string, object?>>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < reader.FieldCount; i++)
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                rows.Add(row);
            }
            return rows;
        }

        private static string? ExtractClientCodeValue(
            IReadOnlyDictionary<string, object?> row,
            string configuredColumnName)
        {
            if (row.TryGetValue(configuredColumnName, out object? configuredColumnValue) && configuredColumnValue is not null)
            {
                string configuredValue = configuredColumnValue.ToString()?.Trim() ?? string.Empty;
                return string.IsNullOrWhiteSpace(configuredValue) ? null : configuredValue;
            }

            if (row.TryGetValue("ClientCode", out object? clientCodeValue) && clientCodeValue is not null)
            {
                string preferredValue = clientCodeValue.ToString()?.Trim() ?? string.Empty;
                return string.IsNullOrWhiteSpace(preferredValue) ? null : preferredValue;
            }

            object? firstValue = row.Values.FirstOrDefault(value => value is not null);
            if (firstValue is null)
            {
                return null;
            }

            string normalizedValue = firstValue.ToString()?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(normalizedValue) ? null : normalizedValue;
        }
    }
}
