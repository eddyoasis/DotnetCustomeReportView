using DataWarehousePower.Models;

namespace DataWarehousePower.Services
{
    public interface IReportExportService
    {
        Task<byte[]> BuildPasswordProtectedZipAsync(
            ReportViewModel report,
            IReadOnlyCollection<string> formats,
            string password,
            string zipSubFileName,
            CsvExportSplitOptions? exportSplitOptions = null,
            CancellationToken cancellationToken = default);
    }
}
