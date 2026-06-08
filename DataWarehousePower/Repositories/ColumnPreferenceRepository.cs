using DataWarehousePower.Data;
using DataWarehousePower.Models;
using Microsoft.EntityFrameworkCore;

namespace DataWarehousePower.Repositories
{
    public class ColumnPreferenceRepository : IColumnPreferenceRepository
    {
        private readonly AppDbContext _context;

        public ColumnPreferenceRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UserColumnPreference?> GetByUserIdAsync(string userId)
            => await _context.UserColumnPreferences
                             .AsNoTracking()
                             .FirstOrDefaultAsync(p => p.UserId == userId);

        public async Task UpsertAsync(UserColumnPreference preference)
        {
            var existing = await _context.UserColumnPreferences
                                         .FirstOrDefaultAsync(p => p.UserId == preference.UserId);

            if (existing is null)
                _context.UserColumnPreferences.Add(preference);
            else
            {
                existing.ColumnJson = preference.ColumnJson;
                _context.UserColumnPreferences.Update(existing);
            }

            await _context.SaveChangesAsync();
        }
    }
}
