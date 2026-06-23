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

        public async Task<ReportManageListViewModel> GetListViewModelAsync(ReportManageFilterViewModel? filter = null)
        {
            var reports = await _repo.GetAllWithColumnsAsync();
            filter ??= new ReportManageFilterViewModel();

            string? search = string.IsNullOrWhiteSpace(filter.Search)
                ? null
                : filter.Search.Trim();

            if (!string.IsNullOrWhiteSpace(search))
            {
                reports = reports.Where(report =>
                        ContainsIgnoreCase(report.ReportName, search) ||
                        ContainsIgnoreCase(report.SourceDatabase, search) ||
                        ContainsIgnoreCase(report.SourceTable, search) ||
                        ContainsIgnoreCase(report.SourceSP, search) ||
                        ContainsIgnoreCase(report.Departments, search) ||
                        report.Columns.Any(column =>
                            ContainsIgnoreCase(column.PropertyName, search) ||
                            ContainsIgnoreCase(column.DefaultLabel, search)))
                    .ToList();
            }

            if (filter.IsActive.HasValue)
            {
                reports = reports.Where(report => report.IsActive == filter.IsActive.Value).ToList();
            }

            return new ReportManageListViewModel
            {
                Filter = filter,
                Reports = reports
            };
        }

        public Task<List<string>> GetSourceDatabaseOptionsAsync()
            => _repo.GetSourceDatabaseOptionsAsync();

        public Task<List<string>> GetSourceTableOptionsAsync(string? sourceDatabase)
            => _repo.GetSourceTableOptionsAsync(sourceDatabase);

        public Task<List<string>> GetSourceStoredProcedureOptionsAsync(string? sourceDatabase)
            => _repo.GetSourceStoredProcedureOptionsAsync(sourceDatabase);

        public Task<List<string>> GetSourceColumnsAsync(string? sourceDatabase, string? sourceTable, string? sourceSP)
            => _repo.GetSourceColumnsAsync(sourceDatabase, sourceTable, sourceSP);

        public Task<List<string>> GetSourceParametersAsync(string? sourceDatabase, string? sourceTable, string? sourceSP)
            => _repo.GetSourceParametersAsync(sourceDatabase, sourceTable, sourceSP);

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

        private static bool ContainsIgnoreCase(string? value, string search)
            => !string.IsNullOrWhiteSpace(value) &&
               value.Contains(search, StringComparison.OrdinalIgnoreCase);

        // ── Mapping helpers ───────────────────────────────────────────────────

        private static ReportManageFormViewModel MapToForm(ReportDefinition r) =>
            new()
            {
                Id             = r.Id,
                ReportName      = r.ReportName,
                SourceDatabase  = r.SourceDatabase,
                SourceTable     = r.SourceTable,
                SourceSP        = r.SourceSP,
                Parameters      = r.Parameters,
                IsActive        = r.IsActive,
                Departments     = r.Departments,
                Columns         = r.Columns.Select(c => new ReportColumnFormModel
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
                Id             = form.Id,
                ReportName      = form.ReportName.Trim(),
                SourceDatabase  = string.IsNullOrWhiteSpace(form.SourceDatabase) ? null : form.SourceDatabase.Trim(),
                SourceTable     = string.IsNullOrWhiteSpace(form.SourceTable) ? null : form.SourceTable.Trim(),
                SourceSP        = string.IsNullOrWhiteSpace(form.SourceSP) ? null : form.SourceSP.Trim(),
                Parameters      = string.IsNullOrWhiteSpace(form.Parameters) ? null : form.Parameters.Trim(),
                IsActive        = form.IsActive,
                Departments     = string.IsNullOrWhiteSpace(form.Departments) ? null : form.Departments.Trim()
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
