using DataWarehousePower.Data;
using DataWarehousePower.Helper;
using DataWarehousePower.Models;
using Microsoft.EntityFrameworkCore;

namespace DataWarehousePower.Repositories
{
    public class ReportConnectionStringRepository : IReportConnectionStringRepository
    {
        private readonly AppDbContext _context;

        public ReportConnectionStringRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<ReportConnectionString>> GetAllAsync()
            => await _context.ReportConnectionStrings
                .AsNoTracking()
                .OrderBy(connection => connection.Name)
                .ThenBy(connection => connection.Id)
                .ToListAsync();

        public async Task<ReportConnectionString?> GetByIdAsync(int id)
            => await _context.ReportConnectionStrings
                .AsNoTracking()
                .FirstOrDefaultAsync(connection => connection.Id == id);

        public async Task<ReportConnectionString> CreateAsync(ReportConnectionString reportConnectionString)
        {
            _context.ReportConnectionStrings.Add(reportConnectionString);
            await _context.SaveChangesAsync();
            return reportConnectionString;
        }

        public async Task UpdateAsync(ReportConnectionString request)
        {
            ReportConnectionString? connection = await _context.ReportConnectionStrings
                .FirstOrDefaultAsync(item => item.Id == request.Id);

            if (connection is null)
            {
                return;
            }

            connection.Name = request.Name;
            connection.Description = request.Description;
            connection.ConnectionString = request.ConnectionString;
            connection.ModifiedBy = request.ModifiedBy;
            connection.ModifiedAt = DateTimeHelper.GetCurrentLocalTime();

            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            ReportConnectionString? connection = await _context.ReportConnectionStrings
                .FirstOrDefaultAsync(item => item.Id == id);

            if (connection is null)
            {
                return;
            }

            _context.ReportConnectionStrings.Remove(connection);
            await _context.SaveChangesAsync();
        }
    }
}
