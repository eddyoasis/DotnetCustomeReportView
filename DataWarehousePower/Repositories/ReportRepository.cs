using DataWarehousePower.Data;
using DataWarehousePower.Models;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;

namespace DataWarehousePower.Repositories
{
    public class ReportRepository : IReportRepository
    {
        private readonly AppDbContext _context;

        public ReportRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<ReportDefinition>> GetAllReportsAsync(string? userDepartment = null)
        {
            List<ReportDefinition> reports = await _context.ReportDefinitions
                .AsNoTracking()
                .Where(r => r.IsActive)
                .OrderBy(r => r.ReportName)
                .ToListAsync();

            return reports
                .Where(report => IsVisibleToDepartment(report.Departments, userDepartment))
                .ToList();
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

            return IsVisibleToDepartment(report.Departments, userDepartment) ? report : null;
        }

        // ── Table mode ────────────────────────────────────────────────────────

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

        private static void AddParam(System.Data.Common.DbCommand cmd, string name, object? value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value         = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }

        private static string EscapeSqlIdentifier(string value)
            => value.Replace("]", "]]", StringComparison.Ordinal);

        private static bool IsVisibleToDepartment(string? reportDepartments, string? userDepartment)
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

            return reportDepartments
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(department => string.Equals(department, normalizedUserDepartment, StringComparison.OrdinalIgnoreCase));
        }
    }
}
