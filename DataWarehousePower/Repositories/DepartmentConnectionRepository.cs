using DataWarehousePower.Data;
using DataWarehousePower.Helper;
using DataWarehousePower.Models;
using Microsoft.EntityFrameworkCore;

namespace DataWarehousePower.Repositories
{
    public class DepartmentConnectionRepository : IDepartmentConnectionRepository
    {
        private readonly AppDbContext _context;

        public DepartmentConnectionRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<DepartmentConnection>> GetAllAsync()
            => await _context.Set<DepartmentConnection>()
                .AsNoTracking()
                .Include(item => item.Department)
                .Include(item => item.ReportConnectionString)
                .OrderBy(item => item.Department != null ? item.Department.Name : string.Empty)
                .ThenBy(item => item.ReportConnectionString != null ? item.ReportConnectionString.Name : string.Empty)
                .ThenBy(item => item.Id)
                .ToListAsync();

        public async Task<DepartmentConnection?> GetByIdAsync(int id)
            => await _context.Set<DepartmentConnection>()
                .AsNoTracking()
                .Include(item => item.Department)
                .Include(item => item.ReportConnectionString)
                .FirstOrDefaultAsync(item => item.Id == id);

        public async Task<DepartmentConnection?> GetByDepartmentIdAsync(int? departmentId)
            => await _context.Set<DepartmentConnection>()
                .AsNoTracking()
                .Include(item => item.Department)
                .Include(item => item.ReportConnectionString)
                .FirstOrDefaultAsync(item => item.DepartmentId == departmentId);

        public async Task<DepartmentConnection> CreateAsync(DepartmentConnection departmentConnection)
        {
            _context.Set<DepartmentConnection>().Add(departmentConnection);
            await _context.SaveChangesAsync();
            return departmentConnection;
        }

        public async Task UpdateAsync(DepartmentConnection request)
        {
            DepartmentConnection? departmentConnection = await _context.Set<DepartmentConnection>()
                .FirstOrDefaultAsync(item => item.Id == request.Id);

            if (departmentConnection is null)
            {
                return;
            }

            departmentConnection.DepartmentId = request.DepartmentId;
            departmentConnection.ReportConnectionStringId = request.ReportConnectionStringId;
            departmentConnection.ModifiedBy = request.ModifiedBy;
            departmentConnection.ModifiedAt = DateTimeHelper.GetCurrentLocalTime();

            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            DepartmentConnection? departmentConnection = await _context.Set<DepartmentConnection>()
                .FirstOrDefaultAsync(item => item.Id == id);

            if (departmentConnection is null)
            {
                return;
            }

            _context.Set<DepartmentConnection>().Remove(departmentConnection);
            await _context.SaveChangesAsync();
        }
    }
}
