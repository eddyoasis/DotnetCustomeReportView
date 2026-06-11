using DataWarehousePower.Models;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;

namespace DataWarehousePower.Services
{
    public sealed class ActiveDirectoryUserService(ILogger<ActiveDirectoryUserService> logger) : IActiveDirectoryUserService
    {
        public Task<AdUserProfile?> GetUserProfileAsync(string? identityName, CancellationToken cancellationToken = default)
        {
            string? samAccountName = ExtractSamAccountName(identityName);
            if (string.IsNullOrWhiteSpace(samAccountName))
            {
                return Task.FromResult<AdUserProfile?>(null);
            }

            if (!OperatingSystem.IsWindows())
            {
                return Task.FromResult<AdUserProfile?>(new AdUserProfile(samAccountName, "N/A", "N/A"));
            }

            try
            {
                using PrincipalContext context = new(ContextType.Domain);
                using UserPrincipal? user = UserPrincipal.FindByIdentity(
                    context,
                    IdentityType.SamAccountName,
                    samAccountName);

                string displayName = string.IsNullOrWhiteSpace(user?.DisplayName) ? samAccountName : user.DisplayName;
                string department = "N/A";
                string role = "N/A";

                if (user?.GetUnderlyingObject() is DirectoryEntry entry)
                {
                    department = ReadDirectoryProperty(entry, "department") ?? "N/A";

                    string? title = ReadDirectoryProperty(entry, "title");
                    role = string.IsNullOrWhiteSpace(title)
                        ? (ReadGroupRole(user) ?? "N/A")
                        : title;
                }

                return Task.FromResult<AdUserProfile?>(new AdUserProfile(displayName, department, role));
            }
            catch (PrincipalServerDownException ex)
            {
                logger.LogWarning(ex, "Unable to contact Active Directory while resolving profile for {IdentityName}.", identityName);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to resolve Active Directory profile for {IdentityName}.", identityName);
            }

            return Task.FromResult<AdUserProfile?>(new AdUserProfile(samAccountName, "N/A", "N/A"));
        }

        private static string? ExtractSamAccountName(string? identityName)
        {
            if (string.IsNullOrWhiteSpace(identityName))
            {
                return null;
            }

            string[] parts = identityName.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 2 ? parts[1] : identityName;
        }

        private static string? ReadDirectoryProperty(DirectoryEntry entry, string propertyName)
        {
            return entry.Properties[propertyName]?.Value?.ToString();
        }

        private string? ReadGroupRole(UserPrincipal user)
        {
            try
            {
                foreach (Principal group in user.GetAuthorizationGroups())
                {
                    string? groupName = group.Name;
                    if (!string.IsNullOrWhiteSpace(groupName))
                    {
                        return groupName;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to resolve AD authorization groups for role lookup.");
                return null;
            }

            return null;
        }
    }
}
