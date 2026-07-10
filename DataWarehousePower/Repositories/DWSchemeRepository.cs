using DataWarehousePower.Data;
using DataWarehousePower.Helper;
using DataWarehousePower.Models;
using Microsoft.EntityFrameworkCore;

namespace DataWarehousePower.Repositories
{
    public class DWSchemeRepository : IDWSchemeRepository
    {
        private readonly AppDbContext _context;

        public DWSchemeRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<DWScheme>> GetAllAsync()
            => await _context.DWSchemes
                .AsNoTracking()
                .OrderBy(item => item.Display)
                .ThenBy(item => item.Id)
                .ToListAsync();

        public async Task<DWScheme?> GetByIdAsync(int id)
            => await _context.DWSchemes
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == id);

        public async Task<DWScheme> CreateAsync(DWScheme dwScheme)
        {
            _context.DWSchemes.Add(dwScheme);
            await _context.SaveChangesAsync();
            return dwScheme;
        }

        public async Task UpdateAsync(DWScheme request)
        {
            DWScheme? dwScheme = await _context.DWSchemes
                .FirstOrDefaultAsync(item => item.Id == request.Id);

            if (dwScheme is null)
            {
                return;
            }

            dwScheme.SP = request.SP;
            dwScheme.Display = request.Display;
            dwScheme.FilterDateColumnName = request.FilterDateColumnName;
            dwScheme.ModifiedBy = request.ModifiedBy;
            dwScheme.ModifiedDatetime = DateTimeHelper.GetCurrentLocalTime();

            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            DWScheme? dwScheme = await _context.DWSchemes
                .FirstOrDefaultAsync(item => item.Id == id);

            if (dwScheme is null)
            {
                return;
            }

            _context.DWSchemes.Remove(dwScheme);
            await _context.SaveChangesAsync();
        }
    }
}
