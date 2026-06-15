using DataWarehousePower.Authorization;
using DataWarehousePower.Models;
using DataWarehousePower.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DataWarehousePower.Controllers;

[Authorize(Policy = DepartmentAuthorizationPolicies.ReportManageAccess)]
public sealed class ScheduledJobController(
    IScheduledReportJobService scheduledReportJobService,
    IColumnPreferenceService columnPreferenceService,
    ILogger<ScheduledJobController> logger) : Controller
{
    public async Task<IActionResult> Index()
    {
        ScheduledJobListViewModel viewModel = await scheduledReportJobService.GetListViewModelAsync();
        return View(viewModel);
    }

    public async Task<IActionResult> Create()
    {
        string userId = columnPreferenceService.ResolveUserId(HttpContext);
        ScheduledJobFormViewModel viewModel = await scheduledReportJobService.GetCreateFormAsync(userId);
        return View("Form", viewModel);
    }

    public async Task<IActionResult> Edit(int id)
    {
        try
        {
            string userId = columnPreferenceService.ResolveUserId(HttpContext);
            ScheduledJobFormViewModel viewModel = await scheduledReportJobService.GetEditFormAsync(id, userId);
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

        if (!ModelState.IsValid)
        {
            ScheduledJobFormViewModel lookupForm = await scheduledReportJobService.GetCreateFormAsync(userId);
            form.AvailableReports = lookupForm.AvailableReports;
            form.AvailableClientCodesByReportId = lookupForm.AvailableClientCodesByReportId;
            form.AvailableClientCodes = lookupForm.AvailableClientCodesByReportId.TryGetValue(form.ReportDefinitionId, out List<string>? reportClientCodes)
                ? reportClientCodes
                : [];
            return View("Form", form);
        }

        string username = ResolveAuditUsername();

        try
        {
            if (form.Id == 0)
            {
                int id = await scheduledReportJobService.CreateAsync(form, userId, username);
                TempData["Success"] = $"Scheduled job created (ID: {id}).";
            }
            else
            {
                await scheduledReportJobService.UpdateAsync(form, userId, username);
                TempData["Success"] = "Scheduled job updated.";
            }

            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Validation failed when saving scheduled job {JobId}", form.Id);
            ModelState.AddModelError(string.Empty, ex.Message);
            ScheduledJobFormViewModel lookupForm = await scheduledReportJobService.GetCreateFormAsync(userId);
            form.AvailableReports = lookupForm.AvailableReports;
            form.AvailableClientCodesByReportId = lookupForm.AvailableClientCodesByReportId;
            form.AvailableClientCodes = lookupForm.AvailableClientCodesByReportId.TryGetValue(form.ReportDefinitionId, out List<string>? reportClientCodes)
                ? reportClientCodes
                : [];
            return View("Form", form);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save scheduled job {JobId}", form.Id);
            ModelState.AddModelError(string.Empty, "An unexpected error occurred while saving the job.");
            ScheduledJobFormViewModel lookupForm = await scheduledReportJobService.GetCreateFormAsync(userId);
            form.AvailableReports = lookupForm.AvailableReports;
            form.AvailableClientCodesByReportId = lookupForm.AvailableClientCodesByReportId;
            form.AvailableClientCodes = lookupForm.AvailableClientCodesByReportId.TryGetValue(form.ReportDefinitionId, out List<string>? reportClientCodes)
                ? reportClientCodes
                : [];
            return View("Form", form);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await scheduledReportJobService.DeleteAsync(id);
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
            ScheduledJobFormViewModel form = await scheduledReportJobService.GetEditFormAsync(id, userId);
            form.IsActive = !form.IsActive;
            await scheduledReportJobService.UpdateAsync(
                form,
                userId,
                ResolveAuditUsername());
            TempData["Success"] = "Scheduled job status updated.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to toggle scheduled job {JobId}", id);
            TempData["Error"] = "Failed to update scheduled job status.";
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
}
