using DataWarehousePower.Models;
using DataWarehousePower.Repositories;

namespace DataWarehousePower.Services
{
    public class ReportManageService : IReportManageService
    {
        private readonly IReportManageRepository _repo;

        public ReportManageService(IReportManageRepository repo)
        {
            _repo = repo;
        }

        public async Task<ReportManageListViewModel> GetListViewModelAsync()
        {
            var reports = await _repo.GetAllWithColumnsAsync();
            return new ReportManageListViewModel { Reports = reports };
        }

        public async Task<ReportManageFormViewModel> GetFormViewModelAsync(int id)
        {
            var report = await _repo.GetByIdWithColumnsAsync(id);
            if (report is null)
                throw new InvalidOperationException($"Report {id} not found.");

            return MapToForm(report);
        }

        public async Task<ReportDefinition> CreateReportAsync(ReportManageFormViewModel form)
        {
            var (report, columns, _) = MapFromForm(form);
            return await _repo.CreateAsync(report, columns);
        }

        public async Task UpdateReportAsync(ReportManageFormViewModel form)
        {
            var (report, columns, deletedIds) = MapFromForm(form);
            await _repo.UpdateAsync(report, columns, deletedIds);
        }

        public Task DeleteReportAsync(int id)
            => _repo.DeleteAsync(id);

        public Task ToggleActiveAsync(int id)
            => _repo.ToggleActiveAsync(id);

        // ── Mapping helpers ───────────────────────────────────────────────────

        private static ReportManageFormViewModel MapToForm(ReportDefinition r) =>
            new()
            {
                Id          = r.Id,
                ReportName  = r.ReportName,
                SourceTable = r.SourceTable,
                SourceSP    = r.SourceSP,
                IsActive    = r.IsActive,
                Columns     = r.Columns.Select(c => new ReportColumnFormModel
                {
                    Id           = c.Id,
                    PropertyName = c.PropertyName,
                    DefaultLabel = c.DefaultLabel,
                    DisplayOrder = c.DisplayOrder
                }).ToList()
            };

        private static (ReportDefinition report, List<ReportColumn> columns, List<int> deletedIds)
            MapFromForm(ReportManageFormViewModel form)
        {
            var report = new ReportDefinition
            {
                Id          = form.Id,
                ReportName  = form.ReportName.Trim(),
                SourceTable = string.IsNullOrWhiteSpace(form.SourceTable) ? null : form.SourceTable.Trim(),
                SourceSP    = string.IsNullOrWhiteSpace(form.SourceSP)    ? null : form.SourceSP.Trim(),
                IsActive    = form.IsActive
            };

            var activeColumns = form.Columns
                .Where(c => !c.IsDeleted)
                .Select(c => new ReportColumn
                {
                    Id           = c.Id,
                    PropertyName = c.PropertyName.Trim(),
                    DefaultLabel = c.DefaultLabel.Trim(),
                    DisplayOrder = c.DisplayOrder
                })
                .ToList();

            var deletedIds = form.Columns
                .Where(c => c.IsDeleted && c.Id > 0)
                .Select(c => c.Id)
                .ToList();

            return (report, activeColumns, deletedIds);
        }
    }
}
