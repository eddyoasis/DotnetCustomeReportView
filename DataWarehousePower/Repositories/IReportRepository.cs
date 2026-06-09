using DataWarehousePower.Models;

namespace DataWarehousePower.Repositories
{
    public interface IReportRepository
    {
        Task<List<ReportDefinition>> GetAllReportsAsync();
        Task<ReportDefinition?> GetReportWithColumnsAsync(int reportId);

        /// <summary>
        /// Fetches data by querying a table directly (SourceTable mode).
        /// Column names are whitelisted against INFORMATION_SCHEMA before use.
        /// </summary>
        Task<List<Dictionary<string, object?>>> GetReportDataFromTableAsync(
            string sourceTable, IEnumerable<string> columnNames);

        /// <summary>
        /// Fetches data by executing a stored procedure (SourceSP mode).
        /// SP name is whitelisted against sys.procedures before use.
        /// </summary>
        Task<List<Dictionary<string, object?>>> GetReportDataFromSpAsync(string sourceSp);
    }
}
