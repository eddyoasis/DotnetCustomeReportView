using System.Security.Claims;
using System.Text.RegularExpressions;

namespace DataWarehousePower.Helper
{
    public static class HttpHelper
    {
        private const int MaxUserIdLen = 128;

        private static readonly Regex _asciiPrintable =
            new(@"^[\x20-\x7E]+$", RegexOptions.Compiled);

        public static string ResolveUserId(HttpContext httpContext)
        {
            string? loginUserId = ExtractLoginUserId(httpContext.User);
            if (IsValidUserId(loginUserId ?? string.Empty))
            {
                string resolvedUserId = loginUserId!;
                return resolvedUserId;
            }
            return string.Empty;
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

        private static bool IsValidUserId(string value)
            => !string.IsNullOrWhiteSpace(value)
               && value.Length <= MaxUserIdLen
               && _asciiPrintable.IsMatch(value);
    }
}
