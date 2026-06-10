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

        public async Task<UserColumnPreference?> GetAsync(string userId, int reportDefinitionId, string? clientCode)
        {
            string normalizedClientCode = NormalizeClientCode(clientCode);

            UserColumnPreference? scopedPreference = await _context.UserColumnPreferences
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId
                                       && p.ReportDefinitionId == reportDefinitionId
                                       && p.ClientCode == normalizedClientCode);

            if (scopedPreference is not null || normalizedClientCode.Length == 0)
            {
                return scopedPreference;
            }

            return await _context.UserColumnPreferences
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId
                                       && p.ReportDefinitionId == reportDefinitionId
                                       && p.ClientCode == string.Empty);
        }

        public async Task<List<string>> GetClientCodesAsync(string userId, int reportDefinitionId)
            => await _context.UserColumnPreferences
                .AsNoTracking()
                .Where(p => p.UserId == userId
                         && p.ReportDefinitionId == reportDefinitionId
                         && p.ClientCode != string.Empty)
                .Select(p => p.ClientCode)
                .Distinct()
                .OrderBy(clientCode => clientCode)
                .ToListAsync();

        public async Task UpsertAsync(UserColumnPreference preference)
        {
            preference.ClientCode = NormalizeClientCode(preference.ClientCode);

            var existing = await _context.UserColumnPreferences
                .FirstOrDefaultAsync(p => p.UserId == preference.UserId
                                       && p.ReportDefinitionId == preference.ReportDefinitionId
                                       && p.ClientCode == preference.ClientCode);
            if (existing is null)
                _context.UserColumnPreferences.Add(preference);
            else
            {
                existing.ColumnJson = preference.ColumnJson;
                _context.UserColumnPreferences.Update(existing);
            }

            await _context.SaveChangesAsync();
        }

        private static string NormalizeClientCode(string? clientCode)
            => clientCode?.Trim() ?? string.Empty;
    }
}
