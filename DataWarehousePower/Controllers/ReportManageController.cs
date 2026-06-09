using DataWarehousePower.Models;
using DataWarehousePower.Services;
using Microsoft.AspNetCore.Mvc;

namespace DataWarehousePower.Controllers
{
    /// <summary>
    /// CRUD management for ReportDefinitions and their ReportColumns.
    /// Route: /ReportManage
    /// </summary>
    public class ReportManageController : Controller
    {
        private readonly IReportManageService _service;
        private readonly ILogger<ReportManageController> _logger;

        public ReportManageController(
            IReportManageService service,
            ILogger<ReportManageController> logger)
        {
            _service = service;
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
                    await _service.CreateReportAsync(form);
                else
                    await _service.UpdateReportAsync(form);

                TempData["Success"] = form.Id == 0
                    ? $"Report \"{form.ReportName}\" created successfully."
                    : $"Report \"{form.ReportName}\" updated successfully.";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save report definition");
                ModelState.AddModelError("", "An error occurred while saving. Please try again.");
                return View("Form", form);
            }
        }

        // POST /ReportManage/Delete/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _service.DeleteReportAsync(id);
                TempData["Success"] = "Report deleted successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete report {ReportId}", id);
                TempData["Error"] = "Failed to delete the report.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
