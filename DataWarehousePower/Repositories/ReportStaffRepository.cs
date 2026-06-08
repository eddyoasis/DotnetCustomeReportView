using DataWarehousePower.Data;
using DataWarehousePower.Models;
using Microsoft.EntityFrameworkCore;

namespace DataWarehousePower.Repositories
{
    public class ReportStaffRepository : IReportStaffRepository
    {
        private readonly AppDbContext _context;

        public ReportStaffRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<ReportStaff>> GetAllAsync()
            => await _context.ReportStaff.AsNoTracking().ToListAsync();

        public async Task<ReportStaff?> GetByIdAsync(int id)
            => await _context.ReportStaff.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
    }
}
