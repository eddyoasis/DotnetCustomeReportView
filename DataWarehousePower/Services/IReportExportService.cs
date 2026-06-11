using DataWarehousePower.Models;

namespace DataWarehousePower.Services
{
    public interface IReportExportService
    {
        Task<byte[]> BuildPasswordProtectedZipAsync(
            ReportViewModel report,
            string format,
            string password,
            CancellationToken cancellationToken = default);
    }
}
