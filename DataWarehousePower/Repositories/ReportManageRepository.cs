using DataWarehousePower.Data;
using DataWarehousePower.Models;
using Microsoft.EntityFrameworkCore;

namespace DataWarehousePower.Repositories
{
    public class ReportManageRepository : IReportManageRepository
    {
        private readonly AppDbContext _context;

        public ReportManageRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<ReportDefinition>> GetAllWithColumnsAsync()
            => await _context.ReportDefinitions
                             .AsNoTracking()
                             .Include(r => r.Columns.OrderBy(c => c.DisplayOrder))
                             .OrderBy(r => r.ReportName)
                             .ToListAsync();

        public async Task<ReportDefinition?> GetByIdWithColumnsAsync(int id)
            => await _context.ReportDefinitions
                             .Include(r => r.Columns.OrderBy(c => c.DisplayOrder))
                             .FirstOrDefaultAsync(r => r.Id == id);

        public async Task<ReportDefinition> CreateAsync(
            ReportDefinition report,
            IEnumerable<ReportColumn> columns)
        {
            _context.ReportDefinitions.Add(report);
            await _context.SaveChangesAsync(); // generate report.Id

            foreach (var col in columns)
            {
                col.ReportDefinitionId = report.Id;
                _context.ReportColumns.Add(col);
            }

            await _context.SaveChangesAsync();
            return report;
        }

        public async Task UpdateAsync(
            ReportDefinition report,
            IEnumerable<ReportColumn> columns,
            IEnumerable<int> deletedColumnIds)
        {
            // Update report header
            var existing = await _context.ReportDefinitions.FindAsync(report.Id)
                           ?? throw new InvalidOperationException($"Report {report.Id} not found.");

            existing.ReportName = report.ReportName;
            existing.SourceDatabase = report.SourceDatabase;
            existing.SourceTable = report.SourceTable;
            existing.SourceSP = report.SourceSP;
            existing.IsActive = report.IsActive;

            // Delete removed columns
            foreach (var colId in deletedColumnIds)
            {
                var col = await _context.ReportColumns.FindAsync(colId);
                if (col != null) _context.ReportColumns.Remove(col);
            }

            // Upsert columns
            foreach (var col in columns)
            {
                if (col.Id == 0)
                {
                    col.ReportDefinitionId = report.Id;
                    _context.ReportColumns.Add(col);
                }
                else
                {
                    var existingCol = await _context.ReportColumns.FindAsync(col.Id);
                    if (existingCol != null)
                    {
                        existingCol.PropertyName = col.PropertyName;
                        existingCol.DefaultLabel = col.DefaultLabel;
                        existingCol.DisplayOrder = col.DisplayOrder;
                    }
                }
            }

            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var report = await _context.ReportDefinitions
                                       .Include(r => r.Columns)
                                       .FirstOrDefaultAsync(r => r.Id == id);
            if (report is null) return;

            _context.ReportColumns.RemoveRange(report.Columns);
            _context.ReportDefinitions.Remove(report);
            await _context.SaveChangesAsync();
        }

        public async Task ToggleActiveAsync(int id)
        {
            var report = await _context.ReportDefinitions.FindAsync(id);
            if (report is null) return;

            report.IsActive = !report.IsActive;
            await _context.SaveChangesAsync();
        }
    }
}
