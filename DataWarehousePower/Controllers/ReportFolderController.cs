using DataWarehousePower.Authorization;
using DataWarehousePower.Models;
using DataWarehousePower.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace DataWarehousePower.Controllers;

[Authorize(Policy = DepartmentAuthorizationPolicies.ReportAccess)]
public sealed class ReportFolderController(
    IConfiguration configuration,
    IColumnPreferenceService columnPreferenceService,
    ILogger<ReportFolderController> logger) : Controller
{
    private const string SaveReportFolderPathSettingKey = "SaveReportFolderPath";

    [HttpGet]
    public IActionResult Index(DateTime? selectedDate = null)
    {
        ReportFolderViewModel viewModel = BuildViewModel(selectedDate);

        if (string.IsNullOrWhiteSpace(viewModel.BasePath))
        {
            TempData["Error"] = $"Configuration '{SaveReportFolderPathSettingKey}' is missing.";
        }

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Open(DateTime? selectedDate = null)
    {
        ReportFolderViewModel viewModel = BuildViewModel(selectedDate);

        if (string.IsNullOrWhiteSpace(viewModel.BasePath))
        {
            TempData["Error"] = $"Configuration '{SaveReportFolderPathSettingKey}' is missing.";
            return RedirectToAction(nameof(Index), new { selectedDate = viewModel.SelectedDate.ToString("yyyy-MM-dd") });
        }

        try
        {
            if (!Directory.Exists(viewModel.FinalPath))
            {
                TempData["Error"] = "The report folder does not exist.";
                return RedirectToAction(nameof(Index), new { selectedDate = viewModel.SelectedDate.ToString("yyyy-MM-dd") });
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{viewModel.FinalPath}\"",
                UseShellExecute = true
            });

            TempData["Success"] = "Folder opened on the application host.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to open report folder in Explorer: {FolderPath}", viewModel.FinalPath);
            TempData["Error"] = "Failed to open report folder.";
        }

        return RedirectToAction(nameof(Index), new { selectedDate = viewModel.SelectedDate.ToString("yyyy-MM-dd") });
    }

    private ReportFolderViewModel BuildViewModel(DateTime? selectedDate)
    {
        DateTime effectiveDate = (selectedDate ?? DateTime.Today).Date;
        string userId = columnPreferenceService.ResolveUserId(HttpContext);
        string userFolder = SanitizePathSegment(userId);
        string basePath = NormalizeBasePath(configuration[SaveReportFolderPathSettingKey]);

        string finalPath = string.IsNullOrWhiteSpace(basePath)
            ? string.Empty
            : Path.Combine(basePath, userFolder, effectiveDate.ToString("yyyy-MM-dd"));

        return new ReportFolderViewModel
        {
            SelectedDate = effectiveDate,
            UserId = userId,
            BasePath = basePath,
            FinalPath = finalPath
        };
    }

    private static string NormalizeBasePath(string? configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return string.Empty;
        }

        return configuredPath.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static string SanitizePathSegment(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return "anonymous";
        }

        char[] invalidChars = Path.GetInvalidFileNameChars();
        string sanitized = new(rawValue
            .Trim()
            .Select(character => invalidChars.Contains(character) ? '_' : character)
            .ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? "anonymous" : sanitized;
    }
}