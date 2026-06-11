using DataWarehousePower.Models;
using System.Text.RegularExpressions;

namespace DataWarehousePower.Services
{
    /// <summary>
    /// Handles cookie-based user identity. Preference persistence is delegated to ReportService.
    /// </summary>
    public class ColumnPreferenceService : IColumnPreferenceService
    {
        private const string CookieName   = "dwp_user_id";
        private const int    CookieDays   = 365;
        private const int    MaxUserIdLen = 128;

        private static readonly Regex _asciiPrintable =
            new(@"^[\x20-\x7E]+$", RegexOptions.Compiled);

        private readonly IReportService _reportService;

        public ColumnPreferenceService(IReportService reportService)
        {
            _reportService = reportService;
        }

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

        public Task<int> SavePreferencesAsync(string userId, int reportId, string? clientCode,
            int? preferenceId,
            IEnumerable<SaveColumnRequest> columns,
            IReadOnlyList<ColumnDefinition> systemColumns)
            => _reportService.SavePreferencesAsync(reportId, userId, clientCode, preferenceId, columns, systemColumns);

        public Task UpdateClientCodeAsync(string userId, int reportId, int preferenceId, string newClientCode)
            => _reportService.UpdateClientCodeAsync(reportId, userId, preferenceId, newClientCode);

        public Task DeletePreferenceAsync(string userId, int reportId, int preferenceId)
            => _reportService.DeletePreferenceAsync(reportId, userId, preferenceId);

        private static bool IsValidUserId(string value)
            => !string.IsNullOrWhiteSpace(value)
               && value.Length <= MaxUserIdLen
               && _asciiPrintable.IsMatch(value);
    }
}
