using DataWarehousePower.Data;
using DataWarehousePower.Helper;
using DataWarehousePower.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace DataWarehousePower.Repositories
{
    public class DataFileManageRepository : IDataFileManageRepository
    {
        private readonly AppDbContext _context;

        public DataFileManageRepository(AppDbContext context)
        {
            _context = context;
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

        private static async Task<List<string>> GetSourceColumnsFromStoredProcedureAsync(
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
                    {
                        continue;
                    }

                    if (seen.Add(name))
                    {
                        items.Add(name);
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

        private static string EscapeSqlIdentifier(string value)
            => value.Replace("]", "]]", StringComparison.Ordinal);

        public async Task<List<DataFileDefinition>> GetAllWithColumnsAsync()
            => await _context.DataFileDefinitions
                .AsNoTracking()
                .Include(dataFile => dataFile.Columns.OrderBy(column => column.DisplayOrder))
                .OrderBy(dataFile => dataFile.DataFileName)
                .ToListAsync();

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
            dataFileDefinition.UserId = dataFileDefinitionRequest.UserId;
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
                        existingColumn.DisplayOrder = column.DisplayOrder;
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
    }
}
