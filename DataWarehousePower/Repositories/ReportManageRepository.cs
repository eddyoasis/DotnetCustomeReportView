using DataWarehousePower.Data;
using DataWarehousePower.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace DataWarehousePower.Repositories
{
    public class ReportManageRepository : IReportManageRepository
    {
        private readonly AppDbContext _context;

        public ReportManageRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<string>> GetSourceDatabaseOptionsAsync()
        {
            var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

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
                    items.Add(reader.GetString(0));
            }

            return items;
        }

        public async Task<List<string>> GetSourceTableOptionsAsync(string? sourceDatabase)
        {
            if (string.IsNullOrWhiteSpace(sourceDatabase))
                return new();

            var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

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
                        items.Add(reader.GetString(0));
                }
            }
            catch (SqlException ex) when (ex.Number is 916 or 229 or 911)
            {
                // Database is invalid or current login cannot access metadata.
                return new();
            }

            return items;
        }

        public async Task<List<string>> GetSourceStoredProcedureOptionsAsync(string? sourceDatabase)
        {
            if (string.IsNullOrWhiteSpace(sourceDatabase))
                return new();

            var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

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
                        items.Add(reader.GetString(0));
                }
            }
            catch (SqlException ex) when (ex.Number is 916 or 229 or 911)
            {
                return new();
            }

            return items;
        }

        public async Task<List<string>> GetSourceColumnsAsync(string? sourceDatabase, string? sourceTable, string? sourceSP)
        {
            if (string.IsNullOrWhiteSpace(sourceDatabase))
                return new();

            var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            var safeDatabase = sourceDatabase.Trim();

            if (!string.IsNullOrWhiteSpace(sourceTable))
                return await GetSourceColumnsFromTableOrViewAsync(conn, safeDatabase, sourceTable.Trim());

            if (!string.IsNullOrWhiteSpace(sourceSP))
                return await GetSourceColumnsFromStoredProcedureAsync(conn, safeDatabase, sourceSP.Trim());

            return new();
        }

        public async Task<List<string>> GetSourceParametersAsync(string? sourceDatabase, string? sourceTable, string? sourceSP)
        {
            if (string.IsNullOrWhiteSpace(sourceDatabase) || string.IsNullOrWhiteSpace(sourceSP))
                return new();

            var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

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
                        items.Add(reader.GetString(0));
                }
            }
            catch (SqlException ex) when (ex.Number is 916 or 229 or 911)
            {
                return new();
            }

            return items;
        }

        private static async Task<List<string>> GetSourceColumnsFromTableOrViewAsync(
            System.Data.Common.DbConnection conn,
            string safeDatabase,
            string sourceTable)
        {
            var escapedDatabase = EscapeSqlIdentifier(safeDatabase);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT COLUMN_NAME " +
                "FROM [" + escapedDatabase + "].INFORMATION_SCHEMA.COLUMNS " +
                "WHERE TABLE_NAME = @tableName " +
                "ORDER BY ORDINAL_POSITION";

            var p = cmd.CreateParameter();
            p.ParameterName = "@tableName";
            p.Value = sourceTable;
            cmd.Parameters.Add(p);

            var items = new List<string>();
            try
            {
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(0))
                        items.Add(reader.GetString(0));
                }
            }
            catch (SqlException ex) when (ex.Number is 916 or 229 or 911)
            {
                return new();
            }

            return items;
        }

        private static async Task<List<string>> GetSourceColumnsFromStoredProcedureAsync(
            System.Data.Common.DbConnection conn,
            string safeDatabase,
            string sourceSP)
        {
            var escapedDatabase = EscapeSqlIdentifier(safeDatabase);
            var escapedProcedure = EscapeSqlIdentifier(sourceSP);

            // Validate procedure exists in selected DB before describing result set.
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
                    return new();
            }

            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "DECLARE @tsql nvarchar(max) = N'EXEC [" + escapedProcedure + "]'; " +
                "EXEC [" + escapedDatabase + "].sys.sp_describe_first_result_set " +
                "@tsql = @tsql, @params = NULL, @browse_information_mode = 0;";

            var items = new List<string>();
            try
            {
                await using var reader = await cmd.ExecuteReaderAsync();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                int nameOrdinal = TryGetOrdinal(reader, "name");
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
                        continue;

                    if (seen.Add(name))
                        items.Add(name);
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

        private static async Task<string?> ValidateDatabaseNameAsync(System.Data.Common.DbConnection conn, string databaseName)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT name FROM sys.databases " +
                "WHERE state_desc = 'ONLINE' AND HAS_DBACCESS(name) = 1 AND name = @name";

            var p = cmd.CreateParameter();
            p.ParameterName = "@name";
            p.Value = databaseName;
            cmd.Parameters.Add(p);

            return await cmd.ExecuteScalarAsync() as string;
        }

        private static string EscapeSqlIdentifier(string value)
            => value.Replace("]", "]]", StringComparison.Ordinal);

        public async Task<List<ReportDefinition>> GetAllWithColumnsAsync()
            => await _context.ReportDefinitions
                             .AsNoTracking()
                             .Include(r => r.Columns.OrderBy(c => c.DisplayOrder))
                             .OrderBy(r => r.ReportName)
                             .ToListAsync();

        public async Task<ReportDefinition?> GetByIdWithColumnsAsync(int id)
            => await _context.ReportDefinitions
                             .Include(r => r.Columns.OrderBy(c => c.DisplayOrder))
                             .FirstOrDefaultAsync(r => r.Id == id);

        public async Task<ReportDefinition> CreateAsync(
            ReportDefinition report,
            IEnumerable<ReportColumn> columns)
        {
            _context.ReportDefinitions.Add(report);
            await _context.SaveChangesAsync(); // generate report.Id

            foreach (var col in columns)
            {
                col.ReportDefinitionId = report.Id;
                _context.ReportColumns.Add(col);
            }

            await _context.SaveChangesAsync();
            return report;
        }

        public async Task UpdateAsync(
            ReportDefinition report,
            IEnumerable<ReportColumn> columns,
            IEnumerable<int> deletedColumnIds)
        {
            // Update report header
            var existing = await _context.ReportDefinitions.FindAsync(report.Id)
                           ?? throw new InvalidOperationException($"Report {report.Id} not found.");

            existing.ReportName = report.ReportName;
            existing.SourceDatabase = report.SourceDatabase;
            existing.SourceTable = report.SourceTable;
            existing.SourceSP = report.SourceSP;
            existing.Parameters = report.Parameters;
            existing.IsActive = report.IsActive;
            existing.Departments = report.Departments;

            // Delete removed columns
            foreach (var colId in deletedColumnIds)
            {
                var col = await _context.ReportColumns.FindAsync(colId);
                if (col != null) _context.ReportColumns.Remove(col);
            }

            // Upsert columns
            foreach (var col in columns)
            {
                if (col.Id == 0)
                {
                    col.ReportDefinitionId = report.Id;
                    _context.ReportColumns.Add(col);
                }
                else
                {
                    var existingCol = await _context.ReportColumns.FindAsync(col.Id);
                    if (existingCol != null)
                    {
                        existingCol.PropertyName = col.PropertyName;
                        existingCol.DefaultLabel = col.DefaultLabel;
                        existingCol.DisplayOrder = col.DisplayOrder;
                        existingCol.MappingParameter = col.MappingParameter;
                    }
                }
            }

            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var report = await _context.ReportDefinitions
                                       .Include(r => r.Columns)
                                       .FirstOrDefaultAsync(r => r.Id == id);
            if (report is null) return;

            _context.ReportColumns.RemoveRange(report.Columns);
            _context.ReportDefinitions.Remove(report);
            await _context.SaveChangesAsync();
        }

        public async Task ToggleActiveAsync(int id)
        {
            var report = await _context.ReportDefinitions.FindAsync(id);
            if (report is null) return;

            report.IsActive = !report.IsActive;
            await _context.SaveChangesAsync();
        }
    }
}
