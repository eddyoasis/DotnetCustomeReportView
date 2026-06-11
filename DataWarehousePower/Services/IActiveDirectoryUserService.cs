using DataWarehousePower.Models;

namespace DataWarehousePower.Services
{
    public interface IActiveDirectoryUserService
    {
        Task<AdUserProfile?> GetUserProfileAsync(string? identityName, CancellationToken cancellationToken = default);
    }
}
