using DataWarehousePower.Authorization;
using DataWarehousePower.Models;
using DataWarehousePower.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DataWarehousePower.Controllers
{
    /// <summary>
    /// CRUD management for ReportDefinitions and their ReportColumns.
    /// Route: /ReportManage
    /// </summary>
    [Authorize(Policy = DepartmentAuthorizationPolicies.ReportManageAccess)]
    public class ReportManageController : Controller
    {
        private readonly IReportManageService _service;
        private readonly IColumnPreferenceService _prefService;
        private readonly IAuditLogService _auditLogService;
        private readonly ILogger<ReportManageController> _logger;

        public ReportManageController(
            IReportManageService service,
            IColumnPreferenceService prefService,
            IAuditLogService auditLogService,
            ILogger<ReportManageController> logger)
        {
            _service = service;
            _prefService = prefService;
            _auditLogService = auditLogService;
            _logger  = logger;
        }

        // GET /ReportManage
        public async Task<IActionResult> Index()
        {
            var vm = await _service.GetListViewModelAsync();
            return View(vm);
        }

        // GET /ReportManage/Create
        public IActionResult Create()
        {
            var vm = new ReportManageFormViewModel
            {
                Columns = new List<ReportColumnFormModel>
                {
                    new() { PropertyName = "", DefaultLabel = "", DisplayOrder = 1 }
                }
            };
            return View("Form", vm);
        }

        // GET /ReportManage/Edit/{id}
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var vm = await _service.GetFormViewModelAsync(id);
                return View("Form", vm);
            }
            catch (InvalidOperationException)
            {
                return NotFound();
            }
        }

        // POST /ReportManage/Save  (handles both Create and Edit)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(ReportManageFormViewModel form)
        {
            string userId = _prefService.ResolveUserId(HttpContext);
            string username = ResolveAuditUsername();
            string correlationId = HttpContext.TraceIdentifier;

            // Remove deleted rows from validation so they don't block the submit
            for (int i = 0; i < form.Columns.Count; i++)
            {
                if (form.Columns[i].IsDeleted)
                {
                    ModelState.Remove($"Columns[{i}].PropertyName");
                    ModelState.Remove($"Columns[{i}].DefaultLabel");
                }
            }

            if (!ModelState.IsValid)
                return View("Form", form);

            // Must have at least one active column
            if (!form.Columns.Any(c => !c.IsDeleted))
            {
                ModelState.AddModelError("", "At least one column is required.");
                return View("Form", form);
            }

            // Must have either SourceTable or SourceSP — not both, not neither
            var hasTable = !string.IsNullOrWhiteSpace(form.SourceTable);
            var hasSP    = !string.IsNullOrWhiteSpace(form.SourceSP);

            if (!hasTable && !hasSP)
            {
                ModelState.AddModelError("", "Provide either a Source Table or a Source Stored Procedure.");
                return View("Form", form);
            }
            if (hasTable && hasSP)
            {
                ModelState.AddModelError("", "Provide either a Source Table or a Source Stored Procedure — not both.");
                return View("Form", form);
            }

            try
            {
                if (form.Id == 0)
                {
                    ReportDefinition created = await _service.CreateReportAsync(form);
                    await _auditLogService.LogActionAsync(
                        actionType: "ReportManageCreateSucceeded",
                        userId: userId,
                        username: username,
                        correlationId: correlationId,
                        entityName: "ReportDefinition",
                        entityId: created.Id.ToString(),
                        entityLabel: created.ReportName,
                        oldValues: null,
                        newValues: new
                        {
                            created.Id,
                            created.ReportName,
                            created.SourceDatabase,
                            created.SourceTable,
                            created.SourceSP,
                            created.IsActive
                        },
                        detail: "Report definition created.");
                }
                else
                {
                    ReportManageFormViewModel? existing = null;
                    try
                    {
                        existing = await _service.GetFormViewModelAsync(form.Id);
                    }
                    catch (InvalidOperationException)
                    {
                        existing = null;
                    }

                    await _service.UpdateReportAsync(form);
                    await _auditLogService.LogActionAsync(
                        actionType: "ReportManageUpdateSucceeded",
                        userId: userId,
                        username: username,
                        correlationId: correlationId,
                        entityName: "ReportDefinition",
                        entityId: form.Id.ToString(),
                        entityLabel: form.ReportName,
                        oldValues: existing is null ? null : new
                        {
                            existing.Id,
                            existing.ReportName,
                            existing.SourceDatabase,
                            existing.SourceTable,
                            existing.SourceSP,
                            existing.IsActive
                        },
                        newValues: new
                        {
                            form.Id,
                            form.ReportName,
                            form.SourceDatabase,
                            form.SourceTable,
                            form.SourceSP,
                            form.IsActive
                        },
                        detail: "Report definition updated.");
                }

                TempData["Success"] = form.Id == 0
                    ? $"Report \"{form.ReportName}\" created successfully."
                    : $"Report \"{form.ReportName}\" updated successfully.";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save report definition");
                await _auditLogService.LogActionAsync(
                    actionType: form.Id == 0 ? "ReportManageCreateFailed" : "ReportManageUpdateFailed",
                    userId: userId,
                    username: username,
                    correlationId: correlationId,
                    entityName: "ReportDefinition",
                    entityId: form.Id == 0 ? null : form.Id.ToString(),
                    entityLabel: form.ReportName,
                    oldValues: null,
                    newValues: new
                    {
                        form.Id,
                        form.ReportName,
                        form.SourceDatabase,
                        form.SourceTable,
                        form.SourceSP,
                        form.IsActive
                    },
                    detail: ex.Message);
                ModelState.AddModelError("", "An error occurred while saving. Please try again.");
                return View("Form", form);
            }
        }

        // POST /ReportManage/Delete/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            string userId = _prefService.ResolveUserId(HttpContext);
            string username = ResolveAuditUsername();
            string correlationId = HttpContext.TraceIdentifier;
            ReportManageFormViewModel? existing = null;

            try
            {
                existing = await _service.GetFormViewModelAsync(id);
            }
            catch (InvalidOperationException)
            {
                existing = null;
            }

            try
            {
                await _service.DeleteReportAsync(id);
                await _auditLogService.LogActionAsync(
                    actionType: "ReportManageDeleteSucceeded",
                    userId: userId,
                    username: username,
                    correlationId: correlationId,
                    entityName: "ReportDefinition",
                    entityId: id.ToString(),
                    entityLabel: existing?.ReportName,
                    oldValues: existing is null ? null : new
                    {
                        existing.Id,
                        existing.ReportName,
                        existing.SourceDatabase,
                        existing.SourceTable,
                        existing.SourceSP,
                        existing.IsActive
                    },
                    newValues: null,
                    detail: "Report definition deleted.");
                TempData["Success"] = "Report deleted successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete report {ReportId}", id);
                await _auditLogService.LogActionAsync(
                    actionType: "ReportManageDeleteFailed",
                    userId: userId,
                    username: username,
                    correlationId: correlationId,
                    entityName: "ReportDefinition",
                    entityId: id.ToString(),
                    entityLabel: existing?.ReportName,
                    oldValues: existing is null ? null : new
                    {
                        existing.Id,
                        existing.ReportName,
                        existing.SourceDatabase,
                        existing.SourceTable,
                        existing.SourceSP,
                        existing.IsActive
                    },
                    newValues: null,
                    detail: ex.Message);
                TempData["Error"] = "Failed to delete the report.";
            }
            return RedirectToAction(nameof(Index));
        }

        // POST /ReportManage/ToggleActive/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            string userId = _prefService.ResolveUserId(HttpContext);
            string username = ResolveAuditUsername();
            string correlationId = HttpContext.TraceIdentifier;
            ReportManageFormViewModel? existing = null;

            try
            {
                existing = await _service.GetFormViewModelAsync(id);
            }
            catch (InvalidOperationException)
            {
                existing = null;
            }

            try
            {
                await _service.ToggleActiveAsync(id);

                bool? newIsActive = existing is null ? null : !existing.IsActive;
                await _auditLogService.LogActionAsync(
                    actionType: "ReportManageToggleActiveSucceeded",
                    userId: userId,
                    username: username,
                    correlationId: correlationId,
                    entityName: "ReportDefinition",
                    entityId: id.ToString(),
                    entityLabel: existing?.ReportName,
                    oldValues: existing is null ? null : new { existing.Id, existing.IsActive },
                    newValues: newIsActive is null ? null : new { Id = id, IsActive = newIsActive.Value },
                    detail: "Report active status toggled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to toggle active status for report {ReportId}", id);
                await _auditLogService.LogActionAsync(
                    actionType: "ReportManageToggleActiveFailed",
                    userId: userId,
                    username: username,
                    correlationId: correlationId,
                    entityName: "ReportDefinition",
                    entityId: id.ToString(),
                    entityLabel: existing?.ReportName,
                    oldValues: existing is null ? null : new { existing.Id, existing.IsActive },
                    newValues: null,
                    detail: ex.Message);
                TempData["Error"] = "Failed to update report status.";
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
}
