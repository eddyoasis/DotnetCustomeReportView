using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;

namespace DataWarehousePower.Services;

public sealed class HangfireDataProtectionService(
    IDataProtectionProvider provider,
    ILogger<HangfireDataProtectionService> logger) : IHangfireDataProtectionService
{
    private readonly IDataProtector _protector = provider.CreateProtector("DataWarehousePower.Hangfire.ScheduledJobs.Password");

    public string Protect(string plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
        {
            throw new InvalidOperationException("Password is required.");
        }

        return _protector.Protect(plainText.Trim());
    }

    public string Unprotect(string cipherText)
    {
        if (string.IsNullOrWhiteSpace(cipherText))
        {
            return string.Empty;
        }

        try
        {
            return _protector.Unprotect(cipherText);
        }
        catch (CryptographicException ex)
        {
            logger.LogWarning(
                ex,
                "Failed to decrypt scheduled job password. Data protection keys may have changed (e.g. after redeployment). " +
                "The password field will be blank and must be re-entered when saving.");
            return string.Empty;
        }
    }
}
