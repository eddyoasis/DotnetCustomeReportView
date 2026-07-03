using DataWarehousePower.Models;
using DataWarehousePower.Repositories;

namespace DataWarehousePower.Services
{
    public class ReportConnectionStringService : IReportConnectionStringService
    {
        private readonly IReportConnectionStringRepository _repository;

        public ReportConnectionStringService(IReportConnectionStringRepository repository)
        {
            _repository = repository;
        }

        public Task<List<ReportConnectionString>> GetAllAsync()
            => _repository.GetAllAsync();

        public Task<ReportConnectionString?> GetByIdAsync(int id)
            => _repository.GetByIdAsync(id);

        public Task<ReportConnectionString> CreateAsync(ReportConnectionString reportConnectionString)
        {
            Normalize(reportConnectionString);
            return _repository.CreateAsync(reportConnectionString);
        }

        public Task UpdateAsync(ReportConnectionString reportConnectionString)
        {
            Normalize(reportConnectionString);
            return _repository.UpdateAsync(reportConnectionString);
        }

        public Task DeleteAsync(int id)
            => _repository.DeleteAsync(id);

        private static void Normalize(ReportConnectionString reportConnectionString)
        {
            reportConnectionString.Name = reportConnectionString.Name.Trim();
            reportConnectionString.Description = string.IsNullOrWhiteSpace(reportConnectionString.Description)
                ? null
                : reportConnectionString.Description.Trim();
            reportConnectionString.ConnectionString = reportConnectionString.ConnectionString.Trim();
        }
    }
}
