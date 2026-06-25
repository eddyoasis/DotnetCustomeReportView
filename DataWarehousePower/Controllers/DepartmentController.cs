using DataWarehousePower.Authorization;
using DataWarehousePower.Helper;
using DataWarehousePower.Models;
using DataWarehousePower.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DataWarehousePower.Controllers
{
    /// <summary>
    /// CRUD management for Departments.
    /// Route: /Department
    /// </summary>
    [Authorize(Policy = DepartmentAuthorizationPolicies.AdminAccess)]
    public class DepartmentController : Controller
    {
        private readonly IDepartmentService _service;
        private readonly ILogger<DepartmentController> _logger;

        public DepartmentController(
            IDepartmentService service,
            ILogger<DepartmentController> logger)
        {
            _service = service;
            _logger = logger;
        }

        // GET /Department
        public async Task<IActionResult> Index()
        {
            var departments = await _service.GetAllAsync();
            return View(departments);
        }

        // GET /Department/Create
        public IActionResult Create()
        {
            return View("Form", new Department());
        }

        // GET /Department/Edit/{id}
        public async Task<IActionResult> Edit(int id)
        {
            var department = await _service.GetByIdAsync(id);
            if (department is null)
                return NotFound();

            return View("Form", department);
        }

        // POST /Department/Save (handles both Create and Edit)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save([Bind("Id,Name,Description,IsActive")] Department department)
        {
            string auditUser = ResolveAuditUser();

            if (department.Id == 0)
            {
                department.CreatedBy = auditUser;
                department.CreatedAt = DateTimeHelper.GetCurrentLocalTime();
            }
            else
            {
                var existing = await _service.GetByIdAsync(department.Id);
                if (existing is null)
                    return NotFound();

                // Keep immutable creation fields from the current persisted record.
                department.CreatedBy = existing.CreatedBy;
                department.CreatedAt = existing.CreatedAt;
                department.ModifiedBy = auditUser;
            }

            // These are server-managed fields and are intentionally not posted from the form.
            ModelState.Remove(nameof(Department.CreatedBy));
            ModelState.Remove(nameof(Department.CreatedAt));
            ModelState.Remove(nameof(Department.ModifiedBy));

            if (!ModelState.IsValid)
                return View("Form", department);

            try
            {
                if (department.Id == 0)
                {
                    await _service.CreateAsync(department);
                    TempData["Success"] = "Department created successfully.";
                }
                else
                {
                    await _service.UpdateAsync(department);
                    TempData["Success"] = "Department updated successfully.";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving department");
                TempData["Error"] = "An error occurred while saving the department.";
                return View("Form", department);
            }
        }

        // POST /Department/Delete/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _service.DeleteAsync(id);
                TempData["Success"] = "Department deleted successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting department");
                TempData["Error"] = "An error occurred while deleting the department.";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST /Department/ToggleActive/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            try
            {
                await _service.ToggleActiveAsync(id);
                TempData["Success"] = "Department status updated successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling department status");
                TempData["Error"] = "An error occurred while updating the department status.";
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

            // Department.CreatedBy/ModifiedBy are limited to 50 characters.
            return user.Length > 50 ? user[..50] : user;
        }
    }
}
