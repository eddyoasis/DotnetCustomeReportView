using DataWarehousePower.Models;
using DataWarehousePower.Repositories;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DataWarehousePower.Services
{
    public class ColumnPreferenceService : IColumnPreferenceService
    {
        private const string CookieName   = "dwp_user_id";
        private const int    CookieDays   = 365;
        private const int    MaxUserIdLen = 128;
        private const int    MaxLabelLen  = 100;

        private static readonly Regex _asciiPrintable =
            new(@"^[\x20-\x7E]+$", RegexOptions.Compiled);

        private static readonly JsonSerializerOptions _jsonOpts =
            new() { PropertyNameCaseInsensitive = true };

        private readonly IColumnPreferenceRepository    _repo;
        private readonly ILogger<ColumnPreferenceService> _logger;

        public ColumnPreferenceService(
            IColumnPreferenceRepository repo,
            ILogger<ColumnPreferenceService> logger)
        {
            _repo   = repo;
            _logger = logger;
        }

        // ── User identity ─────────────────────────────────────────────────────

        public string ResolveUserId(HttpContext httpContext)
        {
            if (httpContext.Request.Cookies.TryGetValue(CookieName, out var existing)
                && IsValidUserId(existing))
                return existing;

            var newId = Guid.NewGuid().ToString();
            httpContext.Response.Cookies.Append(CookieName, newId, new CookieOptions
            {
                Expires     = DateTimeOffset.UtcNow.AddDays(CookieDays),
                HttpOnly    = true,
                SameSite    = SameSiteMode.Lax,
                IsEssential = true
            });
            return newId;
        }

        private static bool IsValidUserId(string value)
            => !string.IsNullOrWhiteSpace(value)
               && value.Length <= MaxUserIdLen
               && _asciiPrintable.IsMatch(value);

        // ── Load preferences ──────────────────────────────────────────────────

        public async Task<List<ColumnDefinition>> LoadPreferencesAsync(
            string userId,
            IReadOnlyList<ColumnDefinition> systemColumns)
        {
            try
            {
                var row = await _repo.GetByUserIdAsync(userId);
                if (row is null) return BuildDefaults(systemColumns);

                var entries = JsonSerializer.Deserialize<List<ColumnJsonEntry>>(
                                  row.ColumnJson, _jsonOpts)
                              ?? new List<ColumnJsonEntry>();

                // Drop stale entries whose PropertyName no longer exists
                var validKeys  = systemColumns.Select(c => c.Key).ToHashSet();
                var validEntries = entries.Where(e => validKeys.Contains(e.PropertyName)).ToList();

                // Fall back to defaults if not every system column has an entry
                if (!systemColumns.All(c => validEntries.Any(e => e.PropertyName == c.Key)))
                    return BuildDefaults(systemColumns);

                return validEntries
                    .OrderBy(e => e.DisplayOrder)
                    .Select(e =>
                    {
                        var sys = systemColumns.First(c => c.Key == e.PropertyName);
                        return new ColumnDefinition
                        {
                            Key          = e.PropertyName,
                            DefaultLabel = sys.DefaultLabel,
                            DisplayLabel = string.IsNullOrWhiteSpace(e.CustomName)
                                               ? sys.DefaultLabel
                                               : e.CustomName,
                            IsVisible    = e.IsVisible,
                            Order        = e.DisplayOrder
                        };
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to load column preferences for user {UserId}. Falling back to defaults.", userId);
                return BuildDefaults(systemColumns);
            }
        }

        // ── Save preferences ──────────────────────────────────────────────────

        public async Task SavePreferencesAsync(
            string userId,
            IEnumerable<SaveColumnRequest> columns,
            IReadOnlyList<ColumnDefinition> systemColumns)
        {
            var validKeys = systemColumns.Select(c => c.Key)
                                         .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var entries = columns
                .Where(c => validKeys.Contains(c.Key))
                .Select(c =>
                {
                    var sys   = systemColumns.First(s =>
                        s.Key.Equals(c.Key, StringComparison.OrdinalIgnoreCase));

                    // Validate label: blank → use default; over limit → truncate
                    var label = string.IsNullOrWhiteSpace(c.DisplayLabel)
                                    ? sys.DefaultLabel
                                    : c.DisplayLabel.Length > MaxLabelLen
                                        ? c.DisplayLabel[..MaxLabelLen]
                                        : c.DisplayLabel;

                    return new ColumnJsonEntry
                    {
                        PropertyName = c.Key,
                        IsVisible    = c.IsVisible,
                        DisplayOrder = Math.Max(1, c.Order),
                        CustomName   = label
                    };
                })
                .ToList();

            var json = JsonSerializer.Serialize(entries);

            await _repo.UpsertAsync(new UserColumnPreference
            {
                UserId     = userId,
                ColumnJson = json
            });
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static List<ColumnDefinition> BuildDefaults(IReadOnlyList<ColumnDefinition> systemColumns)
            => systemColumns.Select((c, i) => new ColumnDefinition
            {
                Key          = c.Key,
                DefaultLabel = c.DefaultLabel,
                DisplayLabel = c.DefaultLabel,
                IsVisible    = true,
                Order        = i + 1
            }).ToList();
    }
}
