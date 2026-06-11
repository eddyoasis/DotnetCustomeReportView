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

        public Task<UserColumnPreference?> GetByIdAsync(string userId, int reportDefinitionId, int preferenceId)
            => _context.UserColumnPreferences
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == preferenceId
                                       && p.UserId == userId
                                       && p.ReportDefinitionId == reportDefinitionId);

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

        public async Task<Dictionary<string, int>> GetClientCodePreferenceIdsAsync(string userId, int reportDefinitionId)
            => await _context.UserColumnPreferences
                .AsNoTracking()
                .Where(p => p.UserId == userId
                         && p.ReportDefinitionId == reportDefinitionId
                         && p.ClientCode != string.Empty)
                .ToDictionaryAsync(p => p.ClientCode, p => p.Id, StringComparer.OrdinalIgnoreCase);

        public async Task<int> UpsertAsync(UserColumnPreference preference, int? preferenceId = null)
        {
            preference.ClientCode = NormalizeClientCode(preference.ClientCode);

            UserColumnPreference? existing;
            if (preferenceId.HasValue)
            {
                existing = await _context.UserColumnPreferences
                    .FirstOrDefaultAsync(p => p.Id == preferenceId.Value
                                           && p.UserId == preference.UserId
                                           && p.ReportDefinitionId == preference.ReportDefinitionId);

                if (existing is null)
                    throw new InvalidOperationException("Preference scope not found.");
            }
            else
            {
                existing = await _context.UserColumnPreferences
                    .FirstOrDefaultAsync(p => p.UserId == preference.UserId
                                           && p.ReportDefinitionId == preference.ReportDefinitionId
                                           && p.ClientCode == preference.ClientCode);
            }

            if (existing is null)
            {
                _context.UserColumnPreferences.Add(preference);
                await _context.SaveChangesAsync();
                return preference.Id;
            }
            else
            {
                bool duplicateScopeExists = await _context.UserColumnPreferences
                    .AnyAsync(p => p.Id != existing.Id
                                && p.UserId == preference.UserId
                                && p.ReportDefinitionId == preference.ReportDefinitionId
                                && p.ClientCode == preference.ClientCode);
                if (duplicateScopeExists)
                    throw new InvalidOperationException("Client code scope already exists.");

                existing.ClientCode = preference.ClientCode;
                existing.ColumnJson = preference.ColumnJson;
                await _context.SaveChangesAsync();
                return existing.Id;
            }
        }

        public async Task UpdateClientCodeAsync(string userId, int reportDefinitionId, int preferenceId, string newClientCode)
        {
            string normalizedClientCode = NormalizeClientCode(newClientCode);
            if (normalizedClientCode.Length == 0)
                throw new InvalidOperationException("Client code cannot be blank.");

            UserColumnPreference? existing = await _context.UserColumnPreferences
                .FirstOrDefaultAsync(p => p.Id == preferenceId
                                       && p.UserId == userId
                                       && p.ReportDefinitionId == reportDefinitionId);

            if (existing is null)
                throw new InvalidOperationException("Preference scope not found.");

            bool duplicateScopeExists = await _context.UserColumnPreferences
                .AnyAsync(p => p.Id != existing.Id
                            && p.UserId == userId
                            && p.ReportDefinitionId == reportDefinitionId
                            && p.ClientCode == normalizedClientCode);
            if (duplicateScopeExists)
                throw new InvalidOperationException("Client code scope already exists.");

            existing.ClientCode = normalizedClientCode;

            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(string userId, int reportDefinitionId, int preferenceId)
        {
            UserColumnPreference? existing = await _context.UserColumnPreferences
                .FirstOrDefaultAsync(p => p.Id == preferenceId
                                       && p.UserId == userId
                                       && p.ReportDefinitionId == reportDefinitionId);

            if (existing is null)
                throw new InvalidOperationException("Preference scope not found.");

            _context.UserColumnPreferences.Remove(existing);

            await _context.SaveChangesAsync();
        }

        private static string NormalizeClientCode(string? clientCode)
            => clientCode?.Trim() ?? string.Empty;
    }
}
