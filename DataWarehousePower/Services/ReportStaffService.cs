using DataWarehousePower.Models;
using DataWarehousePower.Repositories;

namespace DataWarehousePower.Services
{
    public class ReportStaffService : IReportStaffService
    {
        private readonly IReportStaffRepository _repository;

        // System-defined column catalogue — keys never change
        public static readonly IReadOnlyList<ColumnDefinition> SystemColumns =
            new List<ColumnDefinition>
            {
                new() { Key = "Id",   DefaultLabel = "ID",   DisplayLabel = "ID",   IsVisible = true, Order = 1 },
                new() { Key = "Name", DefaultLabel = "Name", DisplayLabel = "Name", IsVisible = true, Order = 2 },
                new() { Key = "Age",  DefaultLabel = "Age",  DisplayLabel = "Age",  IsVisible = true, Order = 3 }
            };

        public ReportStaffService(IReportStaffRepository repository)
        {
            _repository = repository;
        }

        public Task<IEnumerable<ReportStaff>> GetAllStaffAsync()
            => _repository.GetAllAsync();

        public IReadOnlyList<ColumnDefinition> GetAvailableColumns()
            => SystemColumns;
    }
}
