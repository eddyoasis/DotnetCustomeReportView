namespace DataWarehousePower.Services;

public interface IHangfireDataProtectionService
{
    string Protect(string plainText);
    string Unprotect(string cipherText);
}
