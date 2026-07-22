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
    public class DepartmentSnowflakeConnectionController : Controller
    {
        private readonly IDepartmentSnowflakeConnectionService _service;
        private readonly IDepartmentService _departmentService;
        private readonly ISnowflakeConnectionStringService _snowflakeConnectionStringService;
        private readonly ILogger<DepartmentSnowflakeConnectionController> _logger;

        public DepartmentSnowflakeConnectionController(
            IDepartmentSnowflakeConnectionService service,
            IDepartmentService departmentService,
            ISnowflakeConnectionStringService snowflakeConnectionStringService,
            ILogger<DepartmentSnowflakeConnectionController> logger)
        {
            _service = service;
            _departmentService = departmentService;
            _snowflakeConnectionStringService = snowflakeConnectionStringService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            List<DepartmentSnowflakeConnection> departmentConnections = await _service.GetAllAsync();
            return View(departmentConnections);
        }

        public async Task<IActionResult> Create()
        {
            await PopulateLookupsAsync();
            return View("Form", new DepartmentSnowflakeConnection());
        }

        public async Task<IActionResult> Edit(int id)
        {
            DepartmentSnowflakeConnection? departmentConnection = await _service.GetByIdAsync(id);
            if (departmentConnection is null)
            {
                return NotFound();
            }

            await PopulateLookupsAsync(departmentConnection.DepartmentId, departmentConnection.SnowflakeConnectionStringId);
            return View("Form", departmentConnection);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save([Bind("Id,DepartmentId,SnowflakeConnectionStringId")] DepartmentSnowflakeConnection departmentConnection)
        {
            string auditUser = ResolveAuditUser();

            if (departmentConnection.Id == 0)
            {
                departmentConnection.CreatedBy = auditUser;
                departmentConnection.CreatedAt = DateTimeHelper.GetCurrentLocalTime();
            }
            else
            {
                DepartmentSnowflakeConnection? existing = await _service.GetByIdAsync(departmentConnection.Id);
                if (existing is null)
                {
                    return NotFound();
                }

                departmentConnection.CreatedBy = existing.CreatedBy;
                departmentConnection.CreatedAt = existing.CreatedAt;
                departmentConnection.ModifiedBy = auditUser;
            }

            ModelState.Remove(nameof(DepartmentSnowflakeConnection.CreatedBy));
            ModelState.Remove(nameof(DepartmentSnowflakeConnection.CreatedAt));
            ModelState.Remove(nameof(DepartmentSnowflakeConnection.ModifiedBy));

            if (!ModelState.IsValid)
            {
                await PopulateLookupsAsync(departmentConnection.DepartmentId, departmentConnection.SnowflakeConnectionStringId);
                return View("Form", departmentConnection);
            }

            try
            {
                if (departmentConnection.Id == 0)
                {
                    await _service.CreateAsync(departmentConnection);
                    TempData["Success"] = "Department snowflake connection created successfully.";
                }
                else
                {
                    await _service.UpdateAsync(departmentConnection);
                    TempData["Success"] = "Department snowflake connection updated successfully.";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving department snowflake connection");
                TempData["Error"] = "An error occurred while saving the department snowflake connection.";
                await PopulateLookupsAsync(departmentConnection.DepartmentId, departmentConnection.SnowflakeConnectionStringId);
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
                TempData["Success"] = "Department snowflake connection deleted successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting department snowflake connection");
                TempData["Error"] = "An error occurred while deleting the department snowflake connection.";
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateLookupsAsync(int? selectedDepartmentId = null, int? selectedSnowflakeConnectionStringId = null)
        {
            List<Department> departments = await _departmentService.GetAllAsync();
            List<SnowflakeConnectionString> snowflakeConnectionStrings = await _snowflakeConnectionStringService.GetAllAsync();

            ViewBag.Departments = new SelectList(
                departments.OrderBy(item => item.Name),
                nameof(Department.Id),
                nameof(Department.Name),
                selectedDepartmentId);

            ViewBag.SnowflakeConnectionStrings = new SelectList(
                snowflakeConnectionStrings.OrderBy(item => item.Name),
                nameof(SnowflakeConnectionString.Id),
                nameof(SnowflakeConnectionString.Name),
                selectedSnowflakeConnectionStringId);
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