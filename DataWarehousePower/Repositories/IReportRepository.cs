using DataWarehousePower.Models;

namespace DataWarehousePower.Repositories
{
    public interface IReportRepository
    {
        Task<List<ReportDefinition>> GetAllReportsAsync();
        Task<ReportDefinition?> GetReportWithColumnsAsync(int reportId);

        /// <summary>
        /// Dynamically fetches all rows from the report's SourceTable,
        /// returning each row as a property-name → value dictionary.
        /// </summary>
        Task<List<Dictionary<string, object?>>> GetReportDataAsync(string sourceTable, IEnumerable<string> columnNames);
    }
}
