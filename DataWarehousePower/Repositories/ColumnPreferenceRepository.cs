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

        public async Task<UserColumnPreference?> GetAsync(string userId, int reportDefinitionId, string? schemaTemplate)
        {
            string normalizedSchemaTemplate = NormalizeSchemaTemplate(schemaTemplate);

            UserColumnPreference? scopedPreference = await _context.UserColumnPreferences
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId
                                       && p.ReportDefinitionId == reportDefinitionId
                                       && p.SchemaTemplate == normalizedSchemaTemplate);

            if (scopedPreference is not null || normalizedSchemaTemplate.Length == 0)
            {
                return scopedPreference;
            }

            return await _context.UserColumnPreferences
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId
                                       && p.ReportDefinitionId == reportDefinitionId
                                       && p.SchemaTemplate == string.Empty);
        }

        public async Task<UserColumnPreference?> GetDataFileAsync(string userId, int dataFileDefinitionId, string? schemaTemplate)
        {
            string normalizedSchemaTemplate = NormalizeSchemaTemplate(schemaTemplate);

            UserColumnPreference? scopedPreference = await _context.UserColumnPreferences
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId
                                       && p.DataFileDefinitionId == dataFileDefinitionId
                                       && p.SchemaTemplate == normalizedSchemaTemplate);

            if (scopedPreference is not null || normalizedSchemaTemplate.Length == 0)
            {
                return scopedPreference;
            }

            return await _context.UserColumnPreferences
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId
                                       && p.DataFileDefinitionId == dataFileDefinitionId
                                       && p.SchemaTemplate == string.Empty);
        }

        public Task<UserColumnPreference?> GetByIdAsync(string userId, int reportDefinitionId, int preferenceId)
            => _context.UserColumnPreferences
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == preferenceId
                                       && p.UserId == userId
                                       && p.ReportDefinitionId == reportDefinitionId);

        public async Task<List<string>> GetSchemaTemplatesAsync(string userId, int reportDefinitionId)
            => await _context.UserColumnPreferences
                .AsNoTracking()
                .Where(p => p.UserId == userId
                         && p.ReportDefinitionId == reportDefinitionId
                         && p.SchemaTemplate != string.Empty)
                .Select(p => p.SchemaTemplate)
                .Distinct()
                .OrderBy(schemaTemplate => schemaTemplate)
                .ToListAsync();

        public async Task<List<string>> GetDataFileSchemaTemplatesAsync(string userId, int dataFileDefinitionId)
            => await _context.UserColumnPreferences
                .AsNoTracking()
                .Where(p => p.UserId == userId
                         && p.DataFileDefinitionId == dataFileDefinitionId
                         && p.SchemaTemplate != string.Empty)
                .Select(p => p.SchemaTemplate)
                .Distinct()
                .OrderBy(schemaTemplate => schemaTemplate)
                .ToListAsync();

        public async Task<Dictionary<string, int>> GetSchemaTemplatePreferenceIdsAsync(string userId, int reportDefinitionId)
            => await _context.UserColumnPreferences
                .AsNoTracking()
                .Where(p => p.UserId == userId
                         && p.ReportDefinitionId == reportDefinitionId
                         && p.SchemaTemplate != string.Empty)
                .ToDictionaryAsync(p => p.SchemaTemplate, p => p.Id, StringComparer.OrdinalIgnoreCase);

        public async Task<Dictionary<string, int>> GetDataFileSchemaTemplatePreferenceIdsAsync(string userId, int dataFileDefinitionId)
            => await _context.UserColumnPreferences
                .AsNoTracking()
                .Where(p => p.UserId == userId
                         && p.DataFileDefinitionId == dataFileDefinitionId
                         && p.SchemaTemplate != string.Empty)
                .ToDictionaryAsync(p => p.SchemaTemplate, p => p.Id, StringComparer.OrdinalIgnoreCase);

        public async Task<int> UpsertAsync(UserColumnPreference preference, int? preferenceId = null)
        {
            preference.SchemaTemplate = NormalizeSchemaTemplate(preference.SchemaTemplate);

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
                                           && p.SchemaTemplate == preference.SchemaTemplate);
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
                                && p.SchemaTemplate == preference.SchemaTemplate);
                if (duplicateScopeExists)
                    throw new InvalidOperationException("Client code scope already exists.");

                existing.SchemaTemplate = preference.SchemaTemplate;
                existing.ColumnJson = preference.ColumnJson;
                await _context.SaveChangesAsync();
                return existing.Id;
            }
        }

        public async Task UpdateSchemaTemplateAsync(string userId, int reportDefinitionId, int preferenceId, string newSchemaTemplate)
        {
            string normalizedSchemaTemplate = NormalizeSchemaTemplate(newSchemaTemplate);
            if (normalizedSchemaTemplate.Length == 0)
                throw new InvalidOperationException("Client code cannot be blank.");

            UserColumnPreference? existing = await _context.UserColumnPreferences
                .FirstOrDefaultAsync(p => p.Id == preferenceId
                                       && p.UserId == userId
                                       && p.ReportDefinitionId == reportDefinitionId);

            if (existing is null)
                throw new InvalidOperationException("Preference scope not found.");

            string currentSchemaTemplate = existing.SchemaTemplate;
            List<UserColumnPreference> matchingPreferences = await _context.UserColumnPreferences
                .Where(p => p.UserId == userId
                         && p.SchemaTemplate == currentSchemaTemplate)
                .ToListAsync();

            if (matchingPreferences.Count == 0)
                throw new InvalidOperationException("Preference scope not found.");

            List<int> matchingPreferenceIds = matchingPreferences
                .Select(p => p.Id)
                .ToList();
            List<int> matchingReportIds = matchingPreferences
                .Select(p => p.ReportDefinitionId)
                .Distinct()
                .ToList();

            bool duplicateScopeExists = await _context.UserColumnPreferences
                .AnyAsync(p => p.UserId == userId
                            && matchingReportIds.Contains(p.ReportDefinitionId)
                            && p.SchemaTemplate == normalizedSchemaTemplate
                            && !matchingPreferenceIds.Contains(p.Id));
            if (duplicateScopeExists)
                throw new InvalidOperationException("Client code scope already exists for one or more reports.");

            foreach (UserColumnPreference preference in matchingPreferences)
            {
                preference.SchemaTemplate = normalizedSchemaTemplate;
            }

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

        private static string NormalizeSchemaTemplate(string? schemaTemplate)
            => schemaTemplate?.Trim() ?? string.Empty;
    }
}
