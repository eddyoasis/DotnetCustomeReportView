using DataWarehousePower.Models;

namespace DataWarehousePower.Services
{
    public interface IReportExportService
    {
        Task<byte[]> BuildPasswordProtectedZipAsync(
            ReportViewModel report,
            IReadOnlyCollection<string> formats,
            string password,
            CancellationToken cancellationToken = default);
    }
}
