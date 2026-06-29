using DataWarehousePower.Data;
using DataWarehousePower.Helper;
using DataWarehousePower.Models;
using DocumentFormat.OpenXml.Office2010.Excel;
using Microsoft.EntityFrameworkCore;

namespace DataWarehousePower.Repositories
{
    public class DepartmentRepository : IDepartmentRepository
    {
        private readonly AppDbContext _context;

        public DepartmentRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<Department>> GetAllAsync()
            => await _context.Departments.AsNoTracking().ToListAsync();

        public async Task<Department?> GetByIdAsync(int id)
            => await _context.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);

        public async Task<Department> CreateAsync(Department department)
        {
            _context.Departments.Add(department);
            await _context.SaveChangesAsync();
            return department;
        }

        public async Task UpdateAsync(Department departmentReq)
        {
            //_context.Departments.Update(department);
            //await _context.SaveChangesAsync();

            var department = await _context.Departments.FirstOrDefaultAsync(d => d.Id == departmentReq.Id);
            if (department != null)
            {
                department.Name = departmentReq.Name;
                department.Description = departmentReq.Description;
                department.IsActive = departmentReq.IsActive;
                department.ModifiedBy = departmentReq.ModifiedBy;
                department.ModifiedAt = DateTimeHelper.GetCurrentLocalTime();
                await _context.SaveChangesAsync();
            }
        }

        public async Task DeleteAsync(int id)
        {
            var department = await _context.Departments.FirstOrDefaultAsync(d => d.Id == id);
            if (department != null)
            {
                _context.Departments.Remove(department);
                await _context.SaveChangesAsync();
            }
        }

        public async Task ToggleActiveAsync(int id)
        {
            var department = await _context.Departments.FirstOrDefaultAsync(d => d.Id == id);
            if (department != null)
            {
                department.IsActive = !department.IsActive;
                department.ModifiedAt = DateTimeHelper.GetCurrentLocalTime();
                await _context.SaveChangesAsync();
            }
        }
    }
}
