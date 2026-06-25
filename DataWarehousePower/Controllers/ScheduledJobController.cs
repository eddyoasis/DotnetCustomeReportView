using DataWarehousePower.Authorization;
using DataWarehousePower.Models;
using DataWarehousePower.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;

namespace DataWarehousePower.Controllers;

[Authorize(Policy = DepartmentAuthorizationPolicies.ReportManageAccess)]
public sealed class ScheduledJobController(
    IScheduledReportJobService scheduledReportJobService,
    IColumnPreferenceService columnPreferenceService,
    IConfiguration configuration,
    ILogger<ScheduledJobController> logger) : Controller
{
    private const string ExportLocationBasePathsSection = "ScheduledJob:ExportLocationBasePaths";

    public async Task<IActionResult> Index(ScheduledJobFilterViewModel? filter)
    {
        string userId = columnPreferenceService.ResolveUserId(HttpContext);
        ScheduledJobListViewModel viewModel = await scheduledReportJobService.GetListViewModelAsync(userId, filter);
        return View(viewModel);
    }

    public async Task<IActionResult> Create(int? reportDefinitionId = null, string? schemaTemplate = null, string? clientCode = null, List<string>? formats = null, string? returnUrl = null)
    {
        string userId = columnPreferenceService.ResolveUserId(HttpContext);
        string? userDepartment = ResolveUserDepartment();
        ScheduledJobFormViewModel viewModel = await scheduledReportJobService.GetCreateFormAsync(userId, userDepartment);
        viewModel.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;

        if (reportDefinitionId.HasValue && reportDefinitionId.Value > 0)
        {
            viewModel.ReportDefinitionId = reportDefinitionId.Value;
        }

        if (!string.IsNullOrWhiteSpace(schemaTemplate))
        {
            viewModel.SchemaTemplate = schemaTemplate.Trim();
        }

        if (!string.IsNullOrWhiteSpace(clientCode))
        {
            viewModel.ClientCode = clientCode.Trim();
        }

        if (formats is not null && formats.Count > 0)
        {
            List<string> normalizedFormats = formats
                .Where(format => !string.IsNullOrWhiteSpace(format))
                .Select(format => format.Trim().ToLowerInvariant())
                .Where(format => format is "csv" or "excel" or "pdf")
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (normalizedFormats.Count > 0)
            {
                viewModel.Formats = normalizedFormats;
            }
        }

        if (viewModel.ReportDefinitionId > 0 &&
            viewModel.AvailableSchemaTemplatesByReportId.TryGetValue(viewModel.ReportDefinitionId, out List<string>? reportSchemaTemplates))
        {
            viewModel.AvailableSchemaTemplates = reportSchemaTemplates;
        }

        viewModel.AvailableExportLocationBasePathOptions = GetAvailableExportLocationBasePathOptions();
        viewModel.AvailableExportLocationBasePaths = viewModel.AvailableExportLocationBasePathOptions
            .Select(option => option.Path)
            .ToList();

        return View("Form", viewModel);
    }

    public async Task<IActionResult> Edit(int id)
    {
        try
        {
            string userId = columnPreferenceService.ResolveUserId(HttpContext);
            string? userDepartment = ResolveUserDepartment();
            ScheduledJobFormViewModel viewModel = await scheduledReportJobService.GetEditFormAsync(id, userId, userDepartment);
            viewModel.AvailableExportLocationBasePathOptions = GetAvailableExportLocationBasePathOptions();
            viewModel.AvailableExportLocationBasePaths = viewModel.AvailableExportLocationBasePathOptions
                .Select(option => option.Path)
                .ToList();
            return View("Form", viewModel);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(ScheduledJobFormViewModel form)
    {
        string userId = columnPreferenceService.ResolveUserId(HttpContext);

        ValidateExportLocation(form);

        if (!ModelState.IsValid)
        {
            await PopulateFormLookupsAsync(form, userId, ResolveUserDepartment());
            return View("Form", form);
        }

        string username = ResolveAuditUsername();
        string userDepartment = ResolveUserDepartment();

        try
        {
            if (form.Id == 0)
            {
                int id = await scheduledReportJobService.CreateAsync(form, userId, username, userDepartment);
                TempData["Success"] = $"Scheduled job created (ID: {id}).";

                if (Url.IsLocalUrl(form.ReturnUrl))
                {
                    return Redirect(form.ReturnUrl!);
                }
            }
            else
            {
                await scheduledReportJobService.UpdateAsync(form, userId, username, userDepartment);
                TempData["Success"] = "Scheduled job updated.";
            }

            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Validation failed when saving scheduled job {JobId}", form.Id);
            ModelState.AddModelError(string.Empty, ex.Message);
            await PopulateFormLookupsAsync(form, userId, ResolveUserDepartment());
            return View("Form", form);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save scheduled job {JobId}", form.Id);
            ModelState.AddModelError(string.Empty, "An unexpected error occurred while saving the job.");
            await PopulateFormLookupsAsync(form, userId, ResolveUserDepartment());
            return View("Form", form);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            string userId = columnPreferenceService.ResolveUserId(HttpContext);
            await scheduledReportJobService.DeleteAsync(id, userId);
            TempData["Success"] = "Scheduled job deleted.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete scheduled job {JobId}", id);
            TempData["Error"] = "Failed to delete scheduled job.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        try
        {
            string userId = columnPreferenceService.ResolveUserId(HttpContext);
            string? userDepartment = ResolveUserDepartment();
            ScheduledJobFormViewModel form = await scheduledReportJobService.GetEditFormAsync(id, userId, userDepartment);
            form.IsActive = !form.IsActive;
            await scheduledReportJobService.UpdateAsync(
                form,
                userId,
                ResolveAuditUsername(),
                ResolveUserDepartment());
            TempData["Success"] = "Scheduled job status updated.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to toggle scheduled job {JobId}", id);
            TempData["Error"] = "Failed to update scheduled job status.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult OpenExportLocation([FromForm] string? exportLocation, [FromForm] string? returnUrl)
    {
        bool isAjaxRequest = string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

        if (!OperatingSystem.IsWindows())
        {
            return BuildOpenExportLocationErrorResponse(isAjaxRequest, returnUrl, "Opening File Explorer is supported only on Windows hosts.");
        }

        if (!Environment.UserInteractive)
        {
            return BuildOpenExportLocationErrorResponse(
                isAjaxRequest,
                returnUrl,
                "This server session is non-interactive, so File Explorer cannot be opened from UAT.");
        }

        string normalizedExportLocation = NormalizeWindowsPath(exportLocation);
        if (string.IsNullOrWhiteSpace(normalizedExportLocation))
        {
            return BuildOpenExportLocationErrorResponse(isAjaxRequest, returnUrl, "Please select an export location first.");
        }

        List<string> availableBasePaths = GetAvailableExportLocationBasePaths();
        bool isUnderConfiguredBasePath = availableBasePaths
            .Select(NormalizeBasePath)
            .Where(basePath => !string.IsNullOrWhiteSpace(basePath))
            .Any(basePath => normalizedExportLocation.StartsWith(basePath!, StringComparison.OrdinalIgnoreCase));

        if (!isUnderConfiguredBasePath)
        {
            return BuildOpenExportLocationErrorResponse(isAjaxRequest, returnUrl, "Export location must start with one of the configured base paths.");
        }

        if (!Directory.Exists(normalizedExportLocation))
        {
            return BuildOpenExportLocationErrorResponse(isAjaxRequest, returnUrl, "The export location folder does not exist.");
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{normalizedExportLocation}\"",
                UseShellExecute = true
            });

            if (isAjaxRequest)
            {
                return Ok(new { opened = true });
            }

            TempData["Success"] = "Export location opened on the application host.";
            return RedirectToLocalOrIndex(returnUrl);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to open export location in Explorer: {ExportLocation}", normalizedExportLocation);
            return BuildOpenExportLocationErrorResponse(isAjaxRequest, returnUrl, "Failed to open export location.", StatusCodes.Status500InternalServerError);
        }
    }

    private IActionResult BuildOpenExportLocationErrorResponse(bool isAjaxRequest, string? returnUrl, string errorMessage, int statusCode = StatusCodes.Status400BadRequest)
    {
        if (isAjaxRequest)
        {
            return StatusCode(statusCode, errorMessage);
        }

        TempData["Error"] = errorMessage;
        return RedirectToLocalOrIndex(returnUrl);
    }

    private IActionResult RedirectToLocalOrIndex(string? returnUrl)
    {
        if (Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl!);
        }

        return RedirectToAction(nameof(Index));
    }

    private string ResolveAuditUsername()
    {
        string? displayName = HttpContext.Session.GetString("UserDisplayName");
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName.Trim();
        }

        return string.IsNullOrWhiteSpace(User.Identity?.Name) ? "Anonymous" : User.Identity!.Name!.Trim();
    }

    private string? ResolveUserDepartment()
        => HttpContext.Session.GetString("UserDepartment");

    private async Task PopulateFormLookupsAsync(ScheduledJobFormViewModel form, string userId, string? userDepartment)
    {
        ScheduledJobFormViewModel lookupForm = await scheduledReportJobService.GetCreateFormAsync(userId, userDepartment);
        form.AvailableReports = lookupForm.AvailableReports;
        form.AvailableClientCodes = lookupForm.AvailableClientCodes;
        form.AvailableSchemaTemplatesByReportId = lookupForm.AvailableSchemaTemplatesByReportId;
        form.AvailableParametersByReportId = lookupForm.AvailableParametersByReportId;
        form.AvailableSchemaTemplates = lookupForm.AvailableSchemaTemplatesByReportId.TryGetValue(form.ReportDefinitionId, out List<string>? reportClientCodes)
            ? reportClientCodes
            : [];
        form.AvailableExportLocationBasePathOptions = GetAvailableExportLocationBasePathOptions();
        form.AvailableExportLocationBasePaths = form.AvailableExportLocationBasePathOptions
            .Select(option => option.Path)
            .ToList();

        if (form.Id > 0 && string.IsNullOrWhiteSpace(form.ExistingPassword))
        {
            ScheduledJobFormViewModel editForm = await scheduledReportJobService.GetEditFormAsync(form.Id, userId, userDepartment);
            form.ExistingPassword = editForm.ExistingPassword;
        }
    }

    private List<ExportLocationBasePathOptionViewModel> GetAvailableExportLocationBasePathOptions()
    {
        IConfigurationSection section = configuration.GetSection(ExportLocationBasePathsSection);
        List<ExportLocationBasePathOptionViewModel> configuredOptions = section
            .GetChildren()
            .Select(child =>
            {
                string path = (child["Path"] ?? child.Value ?? string.Empty).Trim();
                string label = (child["Label"] ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(path))
                {
                    return null;
                }

                return new ExportLocationBasePathOptionViewModel
                {
                    Path = path,
                    Label = string.IsNullOrWhiteSpace(label) ? path : label
                };
            })
            .Where(option => option is not null)
            .Select(option => option!)
            .ToList();

        return configuredOptions
            .GroupBy(option => option.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(option => option.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<string> GetAvailableExportLocationBasePaths()
    {
        return GetAvailableExportLocationBasePathOptions()
            .Select(option => option.Path)
            .ToList();
    }

    private void ValidateExportLocation(ScheduledJobFormViewModel form)
    {
        List<string> availableBasePaths = GetAvailableExportLocationBasePaths();
        if (availableBasePaths.Count == 0)
        {
            return;
        }

        string normalizedExportLocation = NormalizeWindowsPath(form.ExportLocation);
        if (string.IsNullOrWhiteSpace(normalizedExportLocation))
        {
            ModelState.AddModelError(nameof(form.ExportLocation), "Please select an export location base path.");
            return;
        }

        bool isUnderConfiguredBasePath = availableBasePaths
            .Select(NormalizeBasePath)
            .Where(basePath => !string.IsNullOrWhiteSpace(basePath))
            .Any(basePath => normalizedExportLocation.StartsWith(basePath!, StringComparison.OrdinalIgnoreCase));

        if (!isUnderConfiguredBasePath)
        {
            ModelState.AddModelError(nameof(form.ExportLocation), "Export location must start with one of the configured base paths.");
        }
    }

    private static string NormalizeWindowsPath(string? path)
    {
        string normalizedPath = (path ?? string.Empty).Trim().Replace('/', '\\');

        // Normalize duplicate separators for local drive paths like C:\\temp\\folder.
        if (normalizedPath.Length >= 3 && char.IsLetter(normalizedPath[0]) && normalizedPath[1] == ':')
        {
            string root = normalizedPath[..2];
            string tail = normalizedPath[2..];
            while (tail.Contains("\\\\", StringComparison.Ordinal))
            {
                tail = tail.Replace("\\\\", "\\", StringComparison.Ordinal);
            }

            normalizedPath = root + tail;
        }

        return normalizedPath;
    }

    private static string NormalizeBasePath(string? path)
    {
        string normalizedPath = NormalizeWindowsPath(path);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return string.Empty;
        }

        return normalizedPath.EndsWith('\\') ? normalizedPath : $"{normalizedPath}\\";
    }
}
