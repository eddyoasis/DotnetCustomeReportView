using DataWarehousePower.Authorization;
using DataWarehousePower.Helper;
using DataWarehousePower.Models;
using DataWarehousePower.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace DataWarehousePower.Controllers
{
    [Authorize(Policy = DepartmentAuthorizationPolicies.AdminAccess)]
    public class DepartmentConnectionController : Controller
    {
        private readonly IDepartmentConnectionService _service;
        private readonly IDepartmentService _departmentService;
        private readonly IReportConnectionStringService _reportConnectionStringService;
        private readonly ILogger<DepartmentConnectionController> _logger;

        public DepartmentConnectionController(
            IDepartmentConnectionService service,
            IDepartmentService departmentService,
            IReportConnectionStringService reportConnectionStringService,
            ILogger<DepartmentConnectionController> logger)
        {
            _service = service;
            _departmentService = departmentService;
            _reportConnectionStringService = reportConnectionStringService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            List<DepartmentConnection> departmentConnections = await _service.GetAllAsync();
            return View(departmentConnections);
        }

        public async Task<IActionResult> Create()
        {
            await PopulateLookupsAsync();
            return View("Form", new DepartmentConnection());
        }

        public async Task<IActionResult> Edit(int id)
        {
            DepartmentConnection? departmentConnection = await _service.GetByIdAsync(id);
            if (departmentConnection is null)
            {
                return NotFound();
            }

            await PopulateLookupsAsync(departmentConnection.DepartmentId, departmentConnection.ReportConnectionStringId);
            return View("Form", departmentConnection);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save([Bind("Id,DepartmentId,ReportConnectionStringId")] DepartmentConnection departmentConnection)
        {
            string auditUser = ResolveAuditUser();

            if (departmentConnection.Id == 0)
            {
                departmentConnection.CreatedBy = auditUser;
                departmentConnection.CreatedAt = DateTimeHelper.GetCurrentLocalTime();
            }
            else
            {
                DepartmentConnection? existing = await _service.GetByIdAsync(departmentConnection.Id);
                if (existing is null)
                {
                    return NotFound();
                }

                departmentConnection.CreatedBy = existing.CreatedBy;
                departmentConnection.CreatedAt = existing.CreatedAt;
                departmentConnection.ModifiedBy = auditUser;
            }

            ModelState.Remove(nameof(DepartmentConnection.CreatedBy));
            ModelState.Remove(nameof(DepartmentConnection.CreatedAt));
            ModelState.Remove(nameof(DepartmentConnection.ModifiedBy));

            if (!ModelState.IsValid)
            {
                await PopulateLookupsAsync(departmentConnection.DepartmentId, departmentConnection.ReportConnectionStringId);
                return View("Form", departmentConnection);
            }

            try
            {
                if (departmentConnection.Id == 0)
                {
                    await _service.CreateAsync(departmentConnection);
                    TempData["Success"] = "Department connection created successfully.";
                }
                else
                {
                    await _service.UpdateAsync(departmentConnection);
                    TempData["Success"] = "Department connection updated successfully.";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving department connection");
                TempData["Error"] = "An error occurred while saving the department connection.";
                await PopulateLookupsAsync(departmentConnection.DepartmentId, departmentConnection.ReportConnectionStringId);
                return View("Form", departmentConnection);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _service.DeleteAsync(id);
                TempData["Success"] = "Department connection deleted successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting department connection");
                TempData["Error"] = "An error occurred while deleting the department connection.";
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateLookupsAsync(int? selectedDepartmentId = null, int? selectedReportConnectionStringId = null)
        {
            List<Department> departments = await _departmentService.GetAllAsync();
            List<ReportConnectionString> reportConnectionStrings = await _reportConnectionStringService.GetAllAsync();

            ViewBag.Departments = new SelectList(
                departments.OrderBy(item => item.Name),
                nameof(Department.Id),
                nameof(Department.Name),
                selectedDepartmentId);

            ViewBag.ReportConnectionStrings = new SelectList(
                reportConnectionStrings.OrderBy(item => item.Name),
                nameof(ReportConnectionString.Id),
                nameof(ReportConnectionString.Name),
                selectedReportConnectionStringId);
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
