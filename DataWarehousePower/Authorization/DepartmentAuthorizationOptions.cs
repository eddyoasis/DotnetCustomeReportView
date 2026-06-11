namespace DataWarehousePower.Authorization;

public sealed class DepartmentAuthorizationOptions
{
    public const string SectionName = "DepartmentAuthorization";

    public Dictionary<string, string[]> AllowedDepartmentsByPage { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}