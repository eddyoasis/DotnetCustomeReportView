using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace DataWarehousePower.Authorization;

public sealed class DepartmentAccessRequirement(string pageKey) : IAuthorizationRequirement
{
    public string PageKey { get; } = pageKey;
}

public sealed class DepartmentAccessAuthorizationHandler(
    IOptionsMonitor<DepartmentAuthorizationOptions> optionsMonitor,
    IHttpContextAccessor httpContextAccessor,
    ILogger<DepartmentAccessAuthorizationHandler> logger)
    : AuthorizationHandler<DepartmentAccessRequirement>
{
    private const string DepartmentSessionKey = "UserDepartment";

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        DepartmentAccessRequirement requirement)
    {
        HttpContext? httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return Task.CompletedTask;
        }

        string? department = httpContext.Session.GetString(DepartmentSessionKey);
        if (string.IsNullOrWhiteSpace(department))
        {
            logger.LogWarning("Department is missing in session for user {User}", context.User.Identity?.Name);
            return Task.CompletedTask;
        }

        DepartmentAuthorizationOptions options = optionsMonitor.CurrentValue;
        if (!options.AllowedDepartmentsByPage.TryGetValue(requirement.PageKey, out string[]? allowedDepartments) ||
            allowedDepartments.Length == 0)
        {
            logger.LogWarning("No allowed departments configured for page key {PageKey}", requirement.PageKey);
            return Task.CompletedTask;
        }

        string normalizedDepartment = department.Trim();
        bool isAllowed = allowedDepartments
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Any(value => string.Equals(value.Trim(), normalizedDepartment, StringComparison.OrdinalIgnoreCase));

        if (isAllowed)
        {
            context.Succeed(requirement);
        }
        else
        {
            logger.LogInformation(
                "Access denied for user {User} with department {Department} on page key {PageKey}",
                context.User.Identity?.Name,
                normalizedDepartment,
                requirement.PageKey);
        }

        return Task.CompletedTask;
    }
}