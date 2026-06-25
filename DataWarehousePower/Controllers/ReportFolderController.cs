using DataWarehousePower.Authorization;
using DataWarehousePower.Models;
using DataWarehousePower.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DataWarehousePower.Controllers;

[Authorize(Policy = DepartmentAuthorizationPolicies.ReportAccess)]
public sealed class ReportFolderController(
    IConfiguration configuration,
    IColumnPreferenceService columnPreferenceService,
    ILogger<ReportFolderController> logger) : Controller
{
    private const string IisVirtualDirectoryReportFolderNameSettingKey = "IISVirtualDirectoryReportFoldername";

    [HttpGet]
    public IActionResult Index(DateTime? selectedDate = null)
    {
        ReportFolderViewModel viewModel = BuildViewModel(selectedDate);

        if (string.IsNullOrWhiteSpace(viewModel.VirtualDirectoryName))
        {
            TempData["Error"] = $"Configuration '{IisVirtualDirectoryReportFolderNameSettingKey}' is missing.";
        }

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Open(DateTime? selectedDate = null)
    {
        ReportFolderViewModel viewModel = BuildViewModel(selectedDate);

        if (string.IsNullOrWhiteSpace(viewModel.VirtualDirectoryName))
        {
            TempData["Error"] = $"Configuration '{IisVirtualDirectoryReportFolderNameSettingKey}' is missing.";
            return RedirectToAction(nameof(Index), new { selectedDate = viewModel.SelectedDate.ToString("yyyy-MM-dd") });
        }

        try
        {
            return Redirect(viewModel.FolderUrl);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to redirect to report folder URL: {FolderUrl}", viewModel.FolderUrl);
            TempData["Error"] = "Failed to open report folder URL.";
        }

        return RedirectToAction(nameof(Index), new { selectedDate = viewModel.SelectedDate.ToString("yyyy-MM-dd") });
    }

    private ReportFolderViewModel BuildViewModel(DateTime? selectedDate)
    {
        DateTime effectiveDate = (selectedDate ?? DateTime.Today).Date;
        string userId = columnPreferenceService.ResolveUserId(HttpContext);
        string userPathSegment = SanitizeUrlPathSegment(userId);
        string virtualDirectoryName = NormalizeVirtualDirectoryName(configuration[IisVirtualDirectoryReportFolderNameSettingKey]);

        string folderUrl = string.IsNullOrWhiteSpace(virtualDirectoryName)
            ? string.Empty
            : BuildFolderUrl(virtualDirectoryName, userPathSegment, effectiveDate);

        return new ReportFolderViewModel
        {
            SelectedDate = effectiveDate,
            UserId = userId,
            VirtualDirectoryName = virtualDirectoryName,
            FolderUrl = folderUrl
        };
    }

    private string BuildFolderUrl(string virtualDirectoryName, string userPathSegment, DateTime selectedDate)
    {
        string baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
        string relativePath = $"{virtualDirectoryName}/{Uri.EscapeDataString(userPathSegment)}/{selectedDate:yyyy-MM-dd}";
        return $"{baseUrl}/{relativePath}";
    }

    private static string NormalizeVirtualDirectoryName(string? configuredName)
    {
        if (string.IsNullOrWhiteSpace(configuredName))
        {
            return string.Empty;
        }

        return configuredName.Trim().Trim('/');
    }

    private static string SanitizeUrlPathSegment(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return "anonymous";
        }

        const string allowedChars = "-_.";
        string sanitized = new(rawValue
            .Trim()
            .Select(character => char.IsLetterOrDigit(character) || allowedChars.Contains(character) ? character : '_')
            .ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? "anonymous" : sanitized;
    }
}