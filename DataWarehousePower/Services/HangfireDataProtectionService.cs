using Microsoft.AspNetCore.DataProtection;

namespace DataWarehousePower.Services;

public sealed class HangfireDataProtectionService(IDataProtectionProvider provider) : IHangfireDataProtectionService
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
            throw new InvalidOperationException("Encrypted password is missing.");
        }

        return _protector.Unprotect(cipherText);
    }
}
