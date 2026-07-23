using DataWarehousePower.Models;

namespace DataWarehousePower.Repositories
{
    public interface IReportRepository
    {
        Task<List<ReportDefinition>> GetAllReportsAsync(string? userDepartment = null);
        Task<ReportDefinition?> GetReportWithColumnsAsync(int reportId);
        Task<ReportDefinition?> GetReportWithColumnsAsync(int reportId, string? userDepartment = null);

        Task<List<Dictionary<string, object?>>> GetDataFileDataFromTableAsync(
            string userId,
            string sourceTable,
            IEnumerable<DataFileColumn> columnNames,
            string? sourceDatabase,
            string? filterClientCodeColumn,
            string? filterDateColumn,
            string? clientCode,
            DateTime? dateFrom,
            DateTime? dateTo);

        Task<List<Dictionary<string, object?>>> GetDataFileDataFromTableSnowflakeAsync(
            string userId,
            string userDepartment,
            string sourceTable,
            IEnumerable<DataFileColumn> columnNames,
            string? sourceDatabase,
            string? filterClientCodeColumn,
            string? filterDateColumn,
            string? clientCode,
            DateTime? dateFrom,
            DateTime? dateTo);


        Task<List<Dictionary<string, object?>>> GetDataFileDataFromTableAsync(
            string sourceTable,
            IEnumerable<DataFileColumn> columnNames,
            string? sourceDatabase,
            string? filterClientCodeColumn,
            string? filterDateColumn,
            string? clientCode,
            DateTime? dateFrom,
            DateTime? dateTo);

        Task<List<Dictionary<string, object?>>> GetDataFileDataFromTableSnowflakeAsync(
            string userDepartment,
            string sourceTable,
            IEnumerable<DataFileColumn> columnNames,
            string? sourceDatabase,
            string? filterClientCodeColumn,
            string? filterDateColumn,
            string? clientCode,
            DateTime? dateFrom,
            DateTime? dateTo);

        Task<List<Dictionary<string, object?>>> GetDataFileDataFromTableAsync(
            string sourceTable,
            IEnumerable<DataFileColumn> columnNames,
            string? sourceDatabase,
            DateTime? dateFrom,
            DateTime? dateTo);

        Task<List<Dictionary<string, object?>>> GetDataFileDataFromTableAsync(
            string sourceTable,
            IEnumerable<DataFileColumn> columnNames,
            string? sourceDatabase,
            string? clientCode,
            DateTime? dateFrom,
            DateTime? dateTo);

        Task<List<Dictionary<string, object?>>> GetDataFileDataFromTableAsync(
            string sourceTable,
            IEnumerable<DataFileColumn> columnNames,
            string? sourceDatabase = null);

        Task<List<Dictionary<string, object?>>> GetReportDataFromTableAsync(
            string userId,
            string sourceTable,
            IEnumerable<ReportColumn> columns,
            string? sourceDatabase,
            string? clientCode,
            DateTime? dateFrom,
            DateTime? dateTo);

        Task<List<Dictionary<string, object?>>> GetReportDataFromTableSnowflakeAsync(
            string userId,
            string userDepartment,
            string sourceTable,
            IEnumerable<ReportColumn> columns,
            string? sourceDatabase,
            string? clientCode,
            DateTime? dateFrom,
            DateTime? dateTo);

        Task<List<Dictionary<string, object?>>> GetReportDataFromTableAsync(
            string sourceTable,
            IEnumerable<ReportColumn> columns,
            string? sourceDatabase,
            string? clientCode,
            DateTime? dateFrom,
            DateTime? dateTo);

        Task<List<Dictionary<string, object?>>> GetReportDataFromTableSnowflakeAsync(
            string userDepartment,
            string sourceTable,
            IEnumerable<ReportColumn> columns,
            string? sourceDatabase,
            string? clientCode,
            DateTime? dateFrom,
            DateTime? dateTo);

        /// <summary>
        /// Fetches data by querying a table/view directly (SourceTable mode).
        /// Column names are whitelisted against INFORMATION_SCHEMA before use.
        /// </summary>
        Task<List<Dictionary<string, object?>>> GetReportDataFromTableAsync(
            string sourceTable,
            IEnumerable<string> columnNames,
            string? sourceDatabase = null);

        /// <summary>
        /// Fetches data by executing a stored procedure (SourceSP mode).
        /// SP name is whitelisted against sys.procedures before use.
        /// </summary>
        Task<List<Dictionary<string, object?>>> GetReportDataFromSpAsync(
            string sourceSp,
            string? clientCode = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            IReadOnlyDictionary<string, string?>? parameterValues = null);

        Task<List<string>> GetStoredProcedureParameterNamesAsync(string sourceSp);
        Task<List<string>> GetClientCodesByUserIdAsync(string userId);
        Task<List<string>> GetClientCodeFoldersByUserIdAsync(string userId);
    }
}
