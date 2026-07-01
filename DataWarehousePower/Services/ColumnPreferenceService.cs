using DataWarehousePower.Models;
using System.Security.Claims;
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
            string? loginUserId = ExtractLoginUserId(httpContext.User);
            if (IsValidUserId(loginUserId ?? string.Empty))
            {
                string resolvedUserId = loginUserId!;
                WriteUserCookie(httpContext, resolvedUserId);
                return resolvedUserId;
            }

            if (httpContext.Request.Cookies.TryGetValue(CookieName, out string? existing)
                && IsValidUserId(existing))
            {
                return existing;
            }

            const string fallbackUserId = "anonymous";
            WriteUserCookie(httpContext, fallbackUserId);
            return fallbackUserId;
        }

        public Task<(List<ColumnDefinition> DisplayColumns, int? ActivePreferenceId)> LoadColumnPreferencesAsync(
            string userId,
            int reportId,
            string? schemaTemplate,
            IReadOnlyList<ColumnDefinition> systemColumns)
            => _reportService.LoadColumnPreferencesAsync(userId, reportId, schemaTemplate, systemColumns);

        public Task<(List<ColumnDefinition> DisplayColumns, int? ActivePreferenceId)> LoadDataFileColumnPreferencesAsync(
            string userId,
            int dataFileId,
            string? schemaTemplate,
            IReadOnlyList<ColumnDefinition> systemColumns)
            => _reportService.LoadDataFileColumnPreferencesAsync(userId, dataFileId, schemaTemplate, systemColumns);

        public Task<List<string>> GetSchemaTemplatesAsync(string userId, int reportId)
            => _reportService.GetSchemaTemplatesAsync(userId, reportId);

        public Task<List<string>> GetDataFileSchemaTemplatesAsync(string userId, int dataFileDefinitionId)
            => _reportService.GetDataFileSchemaTemplatesAsync(userId, dataFileDefinitionId);

        public Task<Dictionary<string, int>> GetSchemaTemplatePreferenceIdsAsync(string userId, int reportId)
            => _reportService.GetSchemaTemplatePreferenceIdsAsync(userId, reportId);

        public Task<Dictionary<string, int>> GetDataFileSchemaTemplatePreferenceIdsAsync(string userId, int dataFileDefinitionId)
            => _reportService.GetDataFileSchemaTemplatePreferenceIdsAsync(userId, dataFileDefinitionId);

        public Task<int> SavePreferencesAsync(string userId, int reportId, string? clientCode,
            int? preferenceId,
            IEnumerable<SaveColumnRequest> columns,
            IReadOnlyList<ColumnDefinition> systemColumns)
            => _reportService.SavePreferencesAsync(reportId, userId, clientCode, preferenceId, columns, systemColumns);

        public Task<int> SaveDataFilePreferencesAsync(string userId, int dataFileId, string? clientCode,
            int? preferenceId,
            IEnumerable<SaveColumnRequest> columns,
            IReadOnlyList<ColumnDefinition> systemColumns)
            => _reportService.SaveDataFilePreferencesAsync(dataFileId, userId, clientCode, preferenceId, columns, systemColumns);

        public Task UpdateSchemaTemplateAsync(string userId, int reportId, int preferenceId, string newSchemaTemplate)
            => _reportService.UpdateSchemaTemplateAsync(reportId, userId, preferenceId, newSchemaTemplate);

        public Task DeletePreferenceAsync(string userId, int reportId, int preferenceId)
            => _reportService.DeletePreferenceAsync(reportId, userId, preferenceId);

        private static bool IsValidUserId(string value)
            => !string.IsNullOrWhiteSpace(value)
               && value.Length <= MaxUserIdLen
               && _asciiPrintable.IsMatch(value);

        private static void WriteUserCookie(HttpContext httpContext, string userId)
        {
            httpContext.Response.Cookies.Append(CookieName, userId, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddDays(CookieDays),
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                IsEssential = true
            });
        }

        private static string? ExtractLoginUserId(ClaimsPrincipal? user)
        {
            if (user?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            string? nameIdentifier = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (TryNormalizeIdentity(nameIdentifier, out string? normalizedFromClaim))
            {
                return normalizedFromClaim;
            }

            string? identityName = user.Identity?.Name;
            if (TryNormalizeIdentity(identityName, out string? normalizedFromIdentity))
            {
                return normalizedFromIdentity;
            }

            return null;
        }

        private static bool TryNormalizeIdentity(string? identity, out string? normalized)
        {
            normalized = null;
            if (string.IsNullOrWhiteSpace(identity))
            {
                return false;
            }

            string trimmedIdentity = identity.Trim();
            string candidate = trimmedIdentity;

            int slashIndex = trimmedIdentity.LastIndexOf('\\');
            if (slashIndex >= 0 && slashIndex < trimmedIdentity.Length - 1)
            {
                candidate = trimmedIdentity[(slashIndex + 1)..];
            }
            else
            {
                int atIndex = trimmedIdentity.IndexOf('@');
                if (atIndex > 0)
                {
                    candidate = trimmedIdentity[..atIndex];
                }
            }

            if (!IsValidUserId(candidate))
            {
                return false;
            }

            normalized = candidate;
            return true;
        }
    }
}
