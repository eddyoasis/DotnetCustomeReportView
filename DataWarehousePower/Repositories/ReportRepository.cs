using DataWarehousePower.Data;
using DataWarehousePower.Models;
using Microsoft.EntityFrameworkCore;
using System.Data;

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
                             .OrderBy(r => r.ReportName)
                             .ToListAsync();

        public async Task<ReportDefinition?> GetReportWithColumnsAsync(int reportId)
            => await _context.ReportDefinitions
                             .AsNoTracking()
                             .Include(r => r.Columns)
                             .FirstOrDefaultAsync(r => r.Id == reportId);

        public async Task<List<Dictionary<string, object?>>> GetReportDataAsync(
            string sourceTable,
            IEnumerable<string> columnNames)
        {
            // Open one connection for all three operations (validate table,
            // validate columns, fetch data). GetDbConnection() returns the same
            // underlying object each time, so we must not dispose it between calls.
            var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            // ── Whitelist table name ──────────────────────────────────────────
            var safeTable = await ValidateTableNameAsync(conn, sourceTable);
            if (string.IsNullOrEmpty(safeTable))
                return new List<Dictionary<string, object?>>();

            // ── Whitelist column names ────────────────────────────────────────
            var safeCols = await ValidateColumnNamesAsync(conn, sourceTable, columnNames);
            if (safeCols.Count == 0)
                return new List<Dictionary<string, object?>>();

            // ── Fetch data ────────────────────────────────────────────────────
            var colList = string.Join(", ", safeCols.Select(c => $"[{c}]"));
            var sql     = $"SELECT {colList} FROM [{safeTable}]";

            await using var command = conn.CreateCommand();
            command.CommandText = sql;

            var rows = new List<Dictionary<string, object?>>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < reader.FieldCount; i++)
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                rows.Add(row);
            }

            return rows;
        }

        // ── Private helpers (accept an already-open connection) ───────────────

        private static async Task<string?> ValidateTableNameAsync(
            System.Data.Common.DbConnection conn, string tableName)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES " +
                "WHERE TABLE_NAME = @name AND TABLE_TYPE = 'BASE TABLE'";
            var p = cmd.CreateParameter();
            p.ParameterName = "@name";
            p.Value         = tableName;
            cmd.Parameters.Add(p);

            var result = await cmd.ExecuteScalarAsync();
            return result as string;
        }

        private static async Task<List<string>> ValidateColumnNamesAsync(
            System.Data.Common.DbConnection conn,
            string tableName,
            IEnumerable<string> columns)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS " +
                "WHERE TABLE_NAME = @name";
            var p = cmd.CreateParameter();
            p.ParameterName = "@name";
            p.Value         = tableName;
            cmd.Parameters.Add(p);

            var validColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                validColumns.Add(reader.GetString(0));

            return columns.Where(c => validColumns.Contains(c)).ToList();
        }
    }
}
