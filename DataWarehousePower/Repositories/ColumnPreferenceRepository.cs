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

        public async Task<UserColumnPreference?> GetAsync(string userId, int reportDefinitionId)
            => await _context.UserColumnPreferences
                             .AsNoTracking()
                             .FirstOrDefaultAsync(p => p.UserId == userId
                                                    && p.ReportDefinitionId == reportDefinitionId);

        public async Task UpsertAsync(UserColumnPreference preference)
        {
            var existing = await _context.UserColumnPreferences
                .FirstOrDefaultAsync(p => p.UserId             == preference.UserId
                                       && p.ReportDefinitionId == preference.ReportDefinitionId);
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
