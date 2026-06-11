using DataWarehousePower.Models;
using DataWarehousePower.Services;

namespace DataWarehousePower.Middleware
{
    public sealed class UserDisplayNameSessionMiddleware(RequestDelegate next)
    {
        private const string DisplayNameSessionKey = "UserDisplayName";
        private const string DepartmentSessionKey = "UserDepartment";
        private const string RoleSessionKey = "UserRole";

        public async Task InvokeAsync(HttpContext context, IActiveDirectoryUserService activeDirectoryUserService)
        {
            bool isAuthenticated = context.User?.Identity?.IsAuthenticated == true;
            if (isAuthenticated && string.IsNullOrWhiteSpace(context.Session.GetString(DisplayNameSessionKey)))
            {
                string? identityName = context.User?.Identity?.Name;
                AdUserProfile? profile = await activeDirectoryUserService.GetUserProfileAsync(identityName, context.RequestAborted);

                if (profile is not null)
                {
                    context.Session.SetString(DisplayNameSessionKey, profile.DisplayName);
                    context.Session.SetString(DepartmentSessionKey, profile.Department);
                    context.Session.SetString(RoleSessionKey, profile.Role);
                }
            }

            await next(context);
        }
    }
}
