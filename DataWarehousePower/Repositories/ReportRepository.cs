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

        public async Task<List<ReportDefinition>> GetAllReportsAsync()
            => await _context.ReportDefinitions
                             .AsNoTracking()
                             .Where(r => r.IsActive)
                             .OrderBy(r => r.ReportName)
                             .ToListAsync();

        public async Task<ReportDefinition?> GetReportWithColumnsAsync(int reportId)
            => await _context.ReportDefinitions
                             .AsNoTracking()
                             .Include(r => r.Columns)
                             .FirstOrDefaultAsync(r => r.Id == reportId);

        // ── Table mode ────────────────────────────────────────────────────────

        public async Task<List<Dictionary<string, object?>>> GetReportDataFromTableAsync(
            string sourceTable,
            IEnumerable<string> columnNames)
        {
            var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            var safeTable = await ValidateTableNameAsync(conn, sourceTable);
            if (string.IsNullOrEmpty(safeTable))
                return new();

            var safeCols = await ValidateColumnNamesAsync(conn, sourceTable, columnNames);
            if (safeCols.Count == 0)
                return new();

            var colList = string.Join(", ", safeCols.Select(c => $"[{c}]"));
            var sql     = $"SELECT {colList} FROM [{safeTable}]";

            return await ExecuteReaderAsync(conn, sql, CommandType.Text);
        }

        // ── Stored procedure mode ─────────────────────────────────────────────

        public async Task<List<Dictionary<string, object?>>> GetReportDataFromSpAsync(
            string sourceSp,
            string? clientCode = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null)
        {
            var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            // Whitelist: verify the SP exists in sys.procedures
            var safeSp = await ValidateSpNameAsync(conn, sourceSp);
            if (string.IsNullOrEmpty(safeSp))
                return new();

            var spParameterNames = await GetStoredProcedureParameterNamesAsync(conn, safeSp);
            var parameters = new Dictionary<string, object?>();

            if (spParameterNames.Contains("@ClientCode"))
                parameters["@ClientCode"] = string.IsNullOrWhiteSpace(clientCode) ? DBNull.Value : clientCode;

            if (spParameterNames.Contains("@DateFrom"))
                parameters["@DateFrom"] = dateFrom.HasValue ? dateFrom.Value.Date : DBNull.Value;

            if (spParameterNames.Contains("@DateTo"))
                parameters["@DateTo"] = dateTo.HasValue ? dateTo.Value.Date : DBNull.Value;

            return await ExecuteReaderAsync(conn, $"[{safeSp}]", CommandType.StoredProcedure, parameters);
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

        private static async Task<string?> ValidateTableNameAsync(DbConnection conn, string tableName)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES " +
                "WHERE TABLE_NAME = @name AND TABLE_TYPE = 'BASE TABLE'";
            AddParam(cmd, "@name", tableName);
            return await cmd.ExecuteScalarAsync() as string;
        }

        private static async Task<List<string>> ValidateColumnNamesAsync(
            DbConnection conn, string tableName, IEnumerable<string> columns)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @name";
            AddParam(cmd, "@name", tableName);

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

        private static async Task<HashSet<string>> GetStoredProcedureParameterNamesAsync(DbConnection conn, string spName)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT p.name " +
                "FROM sys.parameters p " +
                "INNER JOIN sys.procedures sp ON p.object_id = sp.object_id " +
                "WHERE sp.name = @name";
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
            p.Value         = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }
    }
}
