using Azure.Core;
using DataWarehousePower.Data;
using DataWarehousePower.Models;
using DataWarehousePower.Models.AppSettings;
using DataWarehousePower.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Data;
using System.Data.Common;
using System.Globalization;

namespace DataWarehousePower.Repositories
{
    public class ReportRepository : IReportRepository
    {
        private readonly AppDbContext _context;
        private readonly ClientCodeLookupOptions _clientCodeLookupOptions;
        private readonly ClientCodeFolderLookupOptions _clientCodeFolderLookupOptions;
        private readonly IDataFileManageRepository _dataFileManageRepository;
        private readonly ISnowflakeService _snowflakeService;

        public ReportRepository(
            AppDbContext context,
            IOptionsSnapshot<ClientCodeLookupOptions> clientCodeLookupOptions,
            IOptionsSnapshot<ClientCodeFolderLookupOptions> clientCodeFolderLookupOptions,
            IDataFileManageRepository dataFileManageRepository,
            ISnowflakeService snowflakeService)
        {
            _context = context;
            _clientCodeLookupOptions = clientCodeLookupOptions.Value;
            _clientCodeFolderLookupOptions = clientCodeFolderLookupOptions.Value;
            _dataFileManageRepository = dataFileManageRepository;
            _snowflakeService = snowflakeService;
        }

        public async Task<List<ReportDefinition>> GetAllReportsAsync(string? userDepartment = null)
        {
            List<ReportDefinition> reports = await _context.ReportDefinitions
                .AsNoTracking()
                .Where(r => r.IsActive)
                .OrderBy(r => r.ReportName)
                .ToListAsync();

            int? userDepartmentId = await ResolveUserDepartmentIdAsync(userDepartment);

            return reports
                .Where(report => IsVisibleToDepartment(report.Departments, userDepartment, userDepartmentId))
                .ToList();
        }

        public async Task<ReportDefinition?> GetReportWithColumnsAsync(int reportId)
        {
            return await _context.ReportDefinitions
                .AsNoTracking()
                .Include(r => r.Columns)
                .FirstOrDefaultAsync(r => r.Id == reportId && r.IsActive);
        }

        public async Task<ReportDefinition?> GetReportWithColumnsAsync(int reportId, string? userDepartment = null)
        {
            ReportDefinition? report = await _context.ReportDefinitions
                .AsNoTracking()
                .Include(r => r.Columns)
                .FirstOrDefaultAsync(r => r.Id == reportId && r.IsActive);

            if (report is null)
            {
                return null;
            }

            int? userDepartmentId = await ResolveUserDepartmentIdAsync(userDepartment);

            return IsVisibleToDepartment(report.Departments, userDepartment, userDepartmentId) ? report : null;
        }

        // ── Table mode ────────────────────────────────────────────────────────

        public async Task<List<Dictionary<string, object?>>> GetDataFileDataFromTableAsync(
            string sourceTable,
            IEnumerable<DataFileColumn> columnNames,
            string? sourceDatabase,
            DateTime? dateFrom,
            DateTime? dateTo)
        {
            var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            var safeDatabase = await ResolveDatabaseNameAsync(conn, sourceDatabase);
            if (safeDatabase is null)
                return new();

            var safeTable = await ValidateTableNameAsync(conn, sourceTable, safeDatabase);
            if (string.IsNullOrEmpty(safeTable))
                return new();

            var safeCols = await ValidateColumnNamesAsync(conn, sourceTable, columnNames.Select(x => x.PropertyName), safeDatabase);
            if (safeCols.Count == 0)
                return new();

            var req = new DataFilePreviewRequest
            {
                SourceDatabase = sourceDatabase, //DataFilePreviewColumnRequest
                SourceTable = sourceTable,
                Columns = columnNames.Select(c => new DataFilePreviewColumnRequest
                {
                    PropertyName = c.PropertyName,
                    MappingParameter = c.MappingParameter,
                    MappingParameterFilter = c.MappingParameterFilter
                }).ToList(),
                IsExport = true
            };
            var result = await _dataFileManageRepository.GetPreviewDataAsync(req, dateFrom, dateTo);
            return result.Rows;
        }

        public async Task<List<Dictionary<string, object?>>> GetDataFileDataFromTableAsync(
            string userId,
            string sourceTable,
            IEnumerable<DataFileColumn> columnNames,
            string? sourceDatabase,
            string? filterClientCodeColumn,
            string? filterDateColumn,
            string? clientCode,
            DateTime? dateFrom,
            DateTime? dateTo)
        {
            var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            var safeDatabase = await ResolveDatabaseNameAsync(conn, sourceDatabase);
            if (safeDatabase is null)
                return new();

            var safeTable = await ValidateTableNameAsync(conn, sourceTable, safeDatabase);
            if (string.IsNullOrEmpty(safeTable))
                return new();

            var safeCols = await ValidateColumnNamesAsync(conn, sourceTable, columnNames.Select(x => x.PropertyName), safeDatabase);
            if (safeCols.Count == 0)
                return new();

            var req = new DataFilePreviewRequest
            {
                UserId = userId,
                SourceDatabase = sourceDatabase, //DataFilePreviewColumnRequest
                SourceTable = sourceTable,
                ClientCode = clientCode,
                Columns = columnNames.Select(c => new DataFilePreviewColumnRequest
                {
                    PropertyName = c.PropertyName,
                    MappingParameter = c.MappingParameter,
                    MappingParameterFilter = c.MappingParameterFilter
                }).ToList(),
                IsExport = true
            };
            var result = await _dataFileManageRepository.GetPreviewDataAsync(req, filterClientCodeColumn, filterDateColumn, dateFrom, dateTo);
            return result.Rows;
        }

        public async Task<List<Dictionary<string, object?>>> GetDataFileDataFromTableAsync(
            string sourceTable,
            IEnumerable<DataFileColumn> columnNames,
            string? sourceDatabase,
            string? filterClientCodeColumn,
            string? filterDateColumn,
            string? clientCode,
            DateTime? dateFrom,
            DateTime? dateTo)
        {
            var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            var safeDatabase = await ResolveDatabaseNameAsync(conn, sourceDatabase);
            if (safeDatabase is null)
                return new();

            var safeTable = await ValidateTableNameAsync(conn, sourceTable, safeDatabase);
            if (string.IsNullOrEmpty(safeTable))
                return new();

            var safeCols = await ValidateColumnNamesAsync(conn, sourceTable, columnNames.Select(x => x.PropertyName), safeDatabase);
            if (safeCols.Count == 0)
                return new();

            var req = new DataFilePreviewRequest
            {
                SourceDatabase = sourceDatabase, //DataFilePreviewColumnRequest
                SourceTable = sourceTable,
                ClientCode = clientCode,
                Columns = columnNames.Select(c => new DataFilePreviewColumnRequest
                {
                    PropertyName = c.PropertyName,
                    MappingParameter = c.MappingParameter,
                    MappingParameterFilter = c.MappingParameterFilter
                }).ToList(),
                IsExport = true
            };
            var result = await _dataFileManageRepository.GetPreviewDataAsync(req, filterClientCodeColumn, filterDateColumn, dateFrom, dateTo);
            return result.Rows;
        }

        public async Task<List<Dictionary<string, object?>>> GetDataFileDataFromTableAsync(
            string sourceTable,
            IEnumerable<DataFileColumn> columnNames,
            string? sourceDatabase,
            string? clientCode,
            DateTime? dateFrom,
            DateTime? dateTo)
        {
            var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            var safeDatabase = await ResolveDatabaseNameAsync(conn, sourceDatabase);
            if (safeDatabase is null)
                return new();

            var safeTable = await ValidateTableNameAsync(conn, sourceTable, safeDatabase);
            if (string.IsNullOrEmpty(safeTable))
                return new();

            var safeCols = await ValidateColumnNamesAsync(conn, sourceTable, columnNames.Select(x => x.PropertyName), safeDatabase);
            if (safeCols.Count == 0)
                return new();

            var req = new DataFilePreviewRequest
            {
                SourceDatabase = sourceDatabase, //DataFilePreviewColumnRequest
                SourceTable = sourceTable,
                ClientCode = clientCode,
                Columns = columnNames.Select(c => new DataFilePreviewColumnRequest
                {
                    PropertyName = c.PropertyName,
                    MappingParameter = c.MappingParameter,
                    MappingParameterFilter = c.MappingParameterFilter
                }).ToList(),
                IsExport = true
            };
            var result = await _dataFileManageRepository.GetPreviewDataAsync(req, dateFrom, dateTo);
            return result.Rows;
        }

        public async Task<List<Dictionary<string, object?>>> GetDataFileDataFromTableAsync(
            string sourceTable,
            IEnumerable<DataFileColumn> columnNames,
            string? sourceDatabase = null)
        {
            var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            var safeDatabase = await ResolveDatabaseNameAsync(conn, sourceDatabase);
            if (safeDatabase is null)
                return new();

            var safeTable = await ValidateTableNameAsync(conn, sourceTable, safeDatabase);
            if (string.IsNullOrEmpty(safeTable))
                return new();

            var safeCols = await ValidateColumnNamesAsync(conn, sourceTable, columnNames.Select(x => x.PropertyName), safeDatabase);
            if (safeCols.Count == 0)
                return new();

            var req = new DataFilePreviewRequest
            {
                SourceDatabase = sourceDatabase, //DataFilePreviewColumnRequest
                SourceTable = sourceTable,
                Columns = columnNames.Select(c => new DataFilePreviewColumnRequest
                {
                    PropertyName = c.PropertyName,
                    MappingParameter = c.MappingParameter,
                    MappingParameterFilter = c.MappingParameterFilter
                }).ToList(),
                IsExport = true
            };
            var result = await _dataFileManageRepository.GetPreviewDataAsync(req);
            return result.Rows;
        }

        public async Task<List<Dictionary<string, object?>>> GetReportDataFromTableAsync(
            string userId,
            string sourceTable,
            IEnumerable<ReportColumn> columns,
            string? sourceDatabase,
            string? clientCode,
            DateTime? dateFrom,
            DateTime? dateTo)
        {
            var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            var safeDatabase = await ResolveDatabaseNameAsync(conn, sourceDatabase);
            if (safeDatabase is null)
                return new();

            var safeTable = await ValidateTableNameAsync(conn, sourceTable, safeDatabase);
            if (string.IsNullOrEmpty(safeTable))
                return new();

            var safeCols = await ValidateColumnNamesAsync(conn, sourceTable, columns.Select(x => x.PropertyName), safeDatabase);
            if (safeCols.Count == 0)
                return new();

            var clintCodes = await GetTRIDsByUserIdAsync(userId);
            var clintCodesForSql = $"({string.Join(",", clintCodes.Select(code => $"'{code}'"))})";

            var whereClauses = new List<string>();
            int parameterIndex = 0;

            Dictionary<string, object?> parameters = new Dictionary<string, object?>();

            string orderBySql = string.Empty;

            foreach (var column in columns.Where(x => !string.IsNullOrEmpty(x.MappingParameter)))
            {
                string parameterName = $"@f{parameterIndex++}";

                if (column.MappingParameter.Contains("ClientCode"))
                {
                    whereClauses.Add($"CAST([{EscapeSqlIdentifier(column.PropertyName)}] AS nvarchar(4000)) LIKE {parameterName}");
                    parameters.Add(parameterName, $"%{clientCode}%");
                }
                else
                {
                    string escapedColumnName = EscapeSqlIdentifier(column.PropertyName);
                    if (dateFrom.HasValue)
                    {
                        whereClauses.Add($"[{escapedColumnName}] >= {parameterName}");
                        parameters.Add(parameterName, dateFrom);
                    }

                    if (dateTo.HasValue)
                    {
                        string endParameterName = $"@f{parameterIndex++}";
                        whereClauses.Add($"[{escapedColumnName}] <= {endParameterName}");
                        parameters.Add(endParameterName, dateTo);
                    }
                    orderBySql = $" ORDER BY {escapedColumnName}";
                }
            }

            if (!string.IsNullOrEmpty(clintCodesForSql))
            {
                whereClauses.Add($"TR_ID in {clintCodesForSql}");
            }

            string whereSql = whereClauses.Count > 0
                ? " WHERE " + string.Join(" AND ", whereClauses)
                : string.Empty;

            var colList = string.Join(", ", safeCols.Select(c => $"[{c}]"));
            var escapedDb = EscapeSqlIdentifier(safeDatabase);
            var sql = $"SELECT {colList} FROM [{escapedDb}]..[{safeTable}] WITH(NOLOCK) {whereSql} {orderBySql}";

            return await ExecuteReaderAsync(conn, sql, CommandType.Text, parameters);
        }

        public async Task<List<Dictionary<string, object?>>> GetReportDataFromTableAsync(
            string sourceTable,
            IEnumerable<ReportColumn> columns,
            string? sourceDatabase,
            string? clientCode,
            DateTime? dateFrom,
            DateTime? dateTo)
        {
            var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            var safeDatabase = await ResolveDatabaseNameAsync(conn, sourceDatabase);
            if (safeDatabase is null)
                return new();

            var safeTable = await ValidateTableNameAsync(conn, sourceTable, safeDatabase);
            if (string.IsNullOrEmpty(safeTable))
                return new();

            var safeCols = await ValidateColumnNamesAsync(conn, sourceTable, columns.Select(x => x.PropertyName), safeDatabase);
            if (safeCols.Count == 0)
                return new();

            var whereClauses = new List<string>();
            int parameterIndex = 0;
            
            Dictionary<string, object?> parameters = new Dictionary<string, object?>();

            string orderBySql = string.Empty;

            foreach (var column in columns.Where(x => !string.IsNullOrEmpty(x.MappingParameter)))
            {
                string parameterName = $"@f{parameterIndex++}";

                if (column.MappingParameter.Contains("ClientCode"))
                {
                    whereClauses.Add($"CAST([{EscapeSqlIdentifier(column.PropertyName)}] AS nvarchar(4000)) LIKE {parameterName}");
                    parameters.Add(parameterName, $"%{clientCode}%");
                }
                else
                {
                    string escapedColumnName = EscapeSqlIdentifier(column.PropertyName);
                    if (dateFrom.HasValue)
                    {
                        whereClauses.Add($"[{escapedColumnName}] >= {parameterName}");
                        parameters.Add(parameterName, dateFrom);
                    }

                    if (dateTo.HasValue)
                    {
                        string endParameterName = $"@f{parameterIndex++}";
                        whereClauses.Add($"[{escapedColumnName}] <= {endParameterName}");
                        parameters.Add(endParameterName, dateTo);
                    }
                    orderBySql = $" ORDER BY {escapedColumnName}";
                }
            }

            string whereSql = whereClauses.Count > 0
                ? " WHERE " + string.Join(" AND ", whereClauses)
                : string.Empty;

            var colList = string.Join(", ", safeCols.Select(c => $"[{c}]"));
            var escapedDb = EscapeSqlIdentifier(safeDatabase);
            var sql = $"SELECT {colList} FROM [{escapedDb}]..[{safeTable}] WITH(NOLOCK) {whereSql} {orderBySql}";

            return await ExecuteReaderAsync(conn, sql, CommandType.Text, parameters);
        }

        public async Task<List<Dictionary<string, object?>>> GetReportDataFromTableSnowflakeAsync(
            string userId,
            string sourceTable,
            IEnumerable<ReportColumn> columns,
            string? sourceDatabase,
            string? clientCode,
            DateTime? dateFrom,
            DateTime? dateTo)
        {
            DataFilePreviewRequest request = BuildSnowflakeReportPreviewRequest(
                userId,
                sourceTable,
                columns,
                sourceDatabase,
                clientCode,
                dateFrom,
                dateTo);

            DataFilePreviewResult result = await _snowflakeService.GetPreviewDataAsync(request);
            return result.Rows;
        }

        public Task<List<Dictionary<string, object?>>> GetReportDataFromTableSnowflakeAsync(
            string sourceTable,
            IEnumerable<ReportColumn> columns,
            string? sourceDatabase,
            string? clientCode,
            DateTime? dateFrom,
            DateTime? dateTo)
            => GetReportDataFromTableSnowflakeAsync(
                string.Empty,
                sourceTable,
                columns,
                sourceDatabase,
                clientCode,
                dateFrom,
                dateTo);

        public async Task<List<Dictionary<string, object?>>> GetReportDataFromTableAsync(
            string sourceTable,
            IEnumerable<string> columnNames,
            string? sourceDatabase = null)
        {
            var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            var safeDatabase = await ResolveDatabaseNameAsync(conn, sourceDatabase);
            if (safeDatabase is null)
                return new();

            var safeTable = await ValidateTableNameAsync(conn, sourceTable, safeDatabase);
            if (string.IsNullOrEmpty(safeTable))
                return new();

            var safeCols = await ValidateColumnNamesAsync(conn, sourceTable, columnNames, safeDatabase);
            if (safeCols.Count == 0)
                return new();

            var colList = string.Join(", ", safeCols.Select(c => $"[{c}]"));
            var escapedDb = EscapeSqlIdentifier(safeDatabase);
            var sql = $"SELECT {colList} FROM [{escapedDb}]..[{safeTable}]";

            return await ExecuteReaderAsync(conn, sql, CommandType.Text);
        }

        // ── Stored procedure mode ─────────────────────────────────────────────

        public async Task<List<Dictionary<string, object?>>> GetReportDataFromSpAsync(
            string sourceSp,
            string? clientCode = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            IReadOnlyDictionary<string, string?>? parameterValues = null)
        {
            var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            // Whitelist: verify the SP exists in sys.procedures
            var safeSp = await ValidateSpNameAsync(conn, sourceSp);
            if (string.IsNullOrEmpty(safeSp))
                return new();

            var spParameterNames = await GetStoredProcedureParameterNamesCoreAsync(conn, safeSp);
            var parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            foreach (string parameterName in spParameterNames)
            {
                if (TryResolveInputParameterValue(parameterValues, parameterName, out string? parameterValue))
                {
                    parameters[parameterName] = string.IsNullOrWhiteSpace(parameterValue)
                        ? DBNull.Value
                        : parameterValue;
                }
            }

            if (spParameterNames.Contains("@ClientCode") && !parameters.ContainsKey("@ClientCode"))
                parameters["@ClientCode"] = string.IsNullOrWhiteSpace(clientCode) ? DBNull.Value : clientCode;

            if (spParameterNames.Contains("@DateFrom") && !parameters.ContainsKey("@DateFrom"))
                parameters["@DateFrom"] = dateFrom.HasValue ? dateFrom.Value.Date : DBNull.Value;

            if (spParameterNames.Contains("@DateTo") && !parameters.ContainsKey("@DateTo"))
                parameters["@DateTo"] = dateTo.HasValue ? dateTo.Value.Date : DBNull.Value;

            return await ExecuteReaderAsync(conn, $"[{safeSp}]", CommandType.StoredProcedure, parameters);
        }

        public async Task<List<string>> GetStoredProcedureParameterNamesAsync(string sourceSp)
        {
            var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            var safeSp = await ValidateSpNameAsync(conn, sourceSp);
            if (string.IsNullOrEmpty(safeSp))
                return new();

            return (await GetStoredProcedureParameterNamesCoreAsync(conn, safeSp)).ToList();
        }

        public async Task<List<string>> GetTRIDsByUserIdAsync(string userId)
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

        public async Task<List<string>> GetClientCodesByUserIdAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return new List<string>();
            }

            string configuredSpName = (_clientCodeLookupOptions.GetCCsStoredProcedureName ?? string.Empty).Trim();
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
                parameters["@tableName"] = "TBL_VW_AUM";
            }

            List<Dictionary<string, object?>> rows = await ExecuteReaderAsync(
                conn,
                $"[{safeSp}]",
                CommandType.StoredProcedure,
                parameters);

            return rows
                .Select(row => ExtractClientCodeValue(row, configuredResponseColumnName))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .SelectMany(value => value!.Split(',', StringSplitOptions.RemoveEmptyEntries)) // split here
                .Select(code => code.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<List<string>> GetClientCodeFoldersByUserIdAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return new List<string>();
            }

            string configuredSpName = (_clientCodeFolderLookupOptions.StoredProcedureName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(configuredSpName))
            {
                return new List<string>();
            }

            string configuredParameterName = NormalizeParameterName(_clientCodeFolderLookupOptions.UserIdParameterName, "@UserId");
            string configuredResponseColumnName = (_clientCodeFolderLookupOptions.ResponseColumnName ?? string.Empty).Trim();
            configuredResponseColumnName = string.IsNullOrWhiteSpace(configuredResponseColumnName)
                ? "ClientCodeFolder"
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

            return rows
                .Select(row => ExtractClientCodeValue(row, configuredResponseColumnName))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // ── Shared reader helper ──────────────────────────────────────────────

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

        // ── Schema validation helpers ─────────────────────────────────────────

        private static async Task<string?> ResolveDatabaseNameAsync(DbConnection conn, string? sourceDatabase)
        {
            var candidate = string.IsNullOrWhiteSpace(sourceDatabase)
                ? conn.Database
                : sourceDatabase.Trim();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT name FROM sys.databases " +
                "WHERE state_desc = 'ONLINE' AND name = @name";
            AddParam(cmd, "@name", candidate);
            return await cmd.ExecuteScalarAsync() as string;
        }

        private static async Task<string?> ValidateTableNameAsync(DbConnection conn, string tableName, string sourceDatabase)
        {
            var escapedDb = EscapeSqlIdentifier(sourceDatabase);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT name FROM [" + escapedDb + "].sys.objects " +
                "WHERE name = @name AND type IN ('U', 'V')";
            AddParam(cmd, "@name", tableName);
            return await cmd.ExecuteScalarAsync() as string;
        }

        private static async Task<List<string>> ValidateColumnNamesAsync(
            DbConnection conn, string tableName, IEnumerable<string> columns, string sourceDatabase)
        {
            var escapedDb = EscapeSqlIdentifier(sourceDatabase);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT name FROM [" + escapedDb + "].sys.columns " +
                "WHERE object_id IN (" +
                "SELECT object_id FROM [" + escapedDb + "].sys.objects WHERE name = @objectName AND type IN ('U', 'V'))";
            AddParam(cmd, "@objectName", tableName);

            var valid = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                valid.Add(reader.GetString(0));

            return columns.Where(c => valid.Contains(c)).ToList();
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

        private static bool TryResolveInputParameterValue(
            IReadOnlyDictionary<string, string?>? parameterValues,
            string parameterName,
            out string? value)
        {
            value = null;
            if (parameterValues is null)
            {
                return false;
            }

            if (parameterValues.TryGetValue(parameterName, out value))
            {
                return true;
            }

            string normalizedName = parameterName.TrimStart('@');
            if (parameterValues.TryGetValue(normalizedName, out value))
            {
                return true;
            }

            string prefixedName = "@" + normalizedName;
            if (parameterValues.TryGetValue(prefixedName, out value))
            {
                return true;
            }

            return false;
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

        private static string NormalizeParameterName(string? parameterName, string fallback)
        {
            string normalized = (parameterName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return fallback;
            }

            return normalized.StartsWith("@", StringComparison.Ordinal) ? normalized : "@" + normalized;
        }

        private static DataFilePreviewRequest BuildSnowflakeReportPreviewRequest(
            string userId,
            string sourceTable,
            IEnumerable<ReportColumn> columns,
            string? sourceDatabase,
            string? clientCode,
            DateTime? dateFrom,
            DateTime? dateTo)
        {
            List<ReportColumn> materializedColumns = columns
                .Where(column => !string.IsNullOrWhiteSpace(column.PropertyName))
                .ToList();

            Dictionary<string, string?> parameters = new(StringComparer.OrdinalIgnoreCase);
            if (dateFrom.HasValue || dateTo.HasValue)
            {
                string start = dateFrom.HasValue
                    ? dateFrom.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                    : string.Empty;
                string end = dateTo.HasValue
                    ? dateTo.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                    : string.Empty;
                string dateRangeFilter = $"{start}|{end}";

                foreach (ReportColumn mappedColumn in materializedColumns.Where(column =>
                             !string.IsNullOrWhiteSpace(column.MappingParameter) &&
                             !column.MappingParameter.Contains("ClientCode", StringComparison.OrdinalIgnoreCase)))
                {
                    string mappingKey = mappedColumn.MappingParameter.Trim();
                    if (!parameters.ContainsKey(mappingKey))
                    {
                        parameters[mappingKey] = dateRangeFilter;
                    }
                }
            }

            return new DataFilePreviewRequest
            {
                UserId = userId,
                SourceDatabase = sourceDatabase,
                SourceTable = sourceTable,
                ClientCode = clientCode,
                DateFrom = dateFrom,
                DateTo = dateTo,
                Parameters = parameters,
                Columns = materializedColumns
                    .Select(column => new DataFilePreviewColumnRequest
                    {
                        PropertyName = column.PropertyName,
                        MappingParameter = column.MappingParameter,
                        MappingParameterFilter = null
                    })
                    .ToList(),
                IsExport = true
            };
        }

        private static void AddParam(System.Data.Common.DbCommand cmd, string name, object? value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value         = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }

        private static string EscapeSqlIdentifier(string value)
            => value.Replace("]", "]]", StringComparison.Ordinal);

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
                return true;
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
    }
}
