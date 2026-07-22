using DataWarehousePower.Data;
using DataWarehousePower.Helper;
using DataWarehousePower.Models;
using Microsoft.EntityFrameworkCore;

namespace DataWarehousePower.Repositories
{
    public class DepartmentSnowflakeConnectionRepository : IDepartmentSnowflakeConnectionRepository
    {
        private readonly AppDbContext _context;

        public DepartmentSnowflakeConnectionRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<DepartmentSnowflakeConnection>> GetAllAsync()
            => await _context.Set<DepartmentSnowflakeConnection>()
                .AsNoTracking()
                .Include(item => item.Department)
                .Include(item => item.SnowflakeConnectionString)
                .OrderBy(item => item.Department != null ? item.Department.Name : string.Empty)
                .ThenBy(item => item.SnowflakeConnectionString != null ? item.SnowflakeConnectionString.Name : string.Empty)
                .ThenBy(item => item.Id)
                .ToListAsync();

        public async Task<DepartmentSnowflakeConnection?> GetByIdAsync(int id)
            => await _context.Set<DepartmentSnowflakeConnection>()
                .AsNoTracking()
                .Include(item => item.Department)
                .Include(item => item.SnowflakeConnectionString)
                .FirstOrDefaultAsync(item => item.Id == id);

        public async Task<DepartmentSnowflakeConnection?> GetByDepartmentIdAsync(int? departmentId)
            => await _context.Set<DepartmentSnowflakeConnection>()
                .AsNoTracking()
                .Include(item => item.Department)
                .Include(item => item.SnowflakeConnectionString)
                .FirstOrDefaultAsync(item => item.DepartmentId == departmentId);

        public async Task<DepartmentSnowflakeConnection> CreateAsync(DepartmentSnowflakeConnection departmentSnowflakeConnection)
        {
            _context.Set<DepartmentSnowflakeConnection>().Add(departmentSnowflakeConnection);
            await _context.SaveChangesAsync();
            return departmentSnowflakeConnection;
        }

        public async Task UpdateAsync(DepartmentSnowflakeConnection request)
        {
            DepartmentSnowflakeConnection? departmentSnowflakeConnection = await _context.Set<DepartmentSnowflakeConnection>()
                .FirstOrDefaultAsync(item => item.Id == request.Id);

            if (departmentSnowflakeConnection is null)
            {
                return;
            }

            departmentSnowflakeConnection.DepartmentId = request.DepartmentId;
            departmentSnowflakeConnection.SnowflakeConnectionStringId = request.SnowflakeConnectionStringId;
            departmentSnowflakeConnection.ModifiedBy = request.ModifiedBy;
            departmentSnowflakeConnection.ModifiedAt = DateTimeHelper.GetCurrentLocalTime();

            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            DepartmentSnowflakeConnection? departmentSnowflakeConnection = await _context.Set<DepartmentSnowflakeConnection>()
                .FirstOrDefaultAsync(item => item.Id == id);

            if (departmentSnowflakeConnection is null)
            {
                return;
            }

            _context.Set<DepartmentSnowflakeConnection>().Remove(departmentSnowflakeConnection);
            await _context.SaveChangesAsync();
        }
    }
}