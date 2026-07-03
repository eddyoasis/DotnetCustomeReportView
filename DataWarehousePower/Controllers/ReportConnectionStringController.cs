using DataWarehousePower.Authorization;
using DataWarehousePower.Helper;
using DataWarehousePower.Models;
using DataWarehousePower.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DataWarehousePower.Controllers
{
    [Authorize(Policy = DepartmentAuthorizationPolicies.AdminAccess)]
    public class ReportConnectionStringController : Controller
    {
        private readonly IReportConnectionStringService _service;
        private readonly ILogger<ReportConnectionStringController> _logger;

        public ReportConnectionStringController(
            IReportConnectionStringService service,
            ILogger<ReportConnectionStringController> logger)
        {
            _service = service;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            List<ReportConnectionString> connectionStrings = await _service.GetAllAsync();
            return View(connectionStrings);
        }

        public IActionResult Create()
        {
            return View("Form", new ReportConnectionString());
        }

        public async Task<IActionResult> Edit(int id)
        {
            ReportConnectionString? connectionString = await _service.GetByIdAsync(id);
            if (connectionString is null)
            {
                return NotFound();
            }

            return View("Form", connectionString);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save([Bind("Id,Name,Description,ConnectionString")] ReportConnectionString reportConnectionString)
        {
            string auditUser = ResolveAuditUser();

            if (reportConnectionString.Id == 0)
            {
                reportConnectionString.CreatedBy = auditUser;
                reportConnectionString.CreatedAt = DateTimeHelper.GetCurrentLocalTime();
            }
            else
            {
                ReportConnectionString? existing = await _service.GetByIdAsync(reportConnectionString.Id);
                if (existing is null)
                {
                    return NotFound();
                }

                reportConnectionString.CreatedBy = existing.CreatedBy;
                reportConnectionString.CreatedAt = existing.CreatedAt;
                reportConnectionString.ModifiedBy = auditUser;
            }

            ModelState.Remove(nameof(ReportConnectionString.CreatedBy));
            ModelState.Remove(nameof(ReportConnectionString.CreatedAt));
            ModelState.Remove(nameof(ReportConnectionString.ModifiedBy));

            if (!ModelState.IsValid)
            {
                return View("Form", reportConnectionString);
            }

            try
            {
                if (reportConnectionString.Id == 0)
                {
                    await _service.CreateAsync(reportConnectionString);
                    TempData["Success"] = "Connection string created successfully.";
                }
                else
                {
                    await _service.UpdateAsync(reportConnectionString);
                    TempData["Success"] = "Connection string updated successfully.";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving report connection string");
                TempData["Error"] = "An error occurred while saving the connection string.";
                return View("Form", reportConnectionString);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _service.DeleteAsync(id);
                TempData["Success"] = "Connection string deleted successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting report connection string");
                TempData["Error"] = "An error occurred while deleting the connection string.";
            }

            return RedirectToAction(nameof(Index));
        }

        private string ResolveAuditUser()
        {
            string user = HttpHelper.ResolveUserId(HttpContext);
            if (string.IsNullOrWhiteSpace(user))
            {
                user = User.Identity?.Name ?? "System";
            }

            return user.Length > 50 ? user[..50] : user;
        }
    }
}
