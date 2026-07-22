using DataWarehousePower.Authorization;
using DataWarehousePower.Helper;
using DataWarehousePower.Models;
using DataWarehousePower.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DataWarehousePower.Controllers
{
    [Authorize(Policy = DepartmentAuthorizationPolicies.AdminAccess)]
    public class SnowflakeConnectionStringController : Controller
    {
        private readonly ISnowflakeConnectionStringService _service;
        private readonly ILogger<SnowflakeConnectionStringController> _logger;

        public SnowflakeConnectionStringController(
            ISnowflakeConnectionStringService service,
            ILogger<SnowflakeConnectionStringController> logger)
        {
            _service = service;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            List<SnowflakeConnectionString> connectionStrings = await _service.GetAllAsync();
            return View(connectionStrings);
        }

        public IActionResult Create()
        {
            return View("Form", new SnowflakeConnectionString());
        }

        public async Task<IActionResult> Edit(int id)
        {
            SnowflakeConnectionString? connectionString = await _service.GetByIdAsync(id);
            if (connectionString is null)
            {
                return NotFound();
            }

            return View("Form", connectionString);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save([Bind("Id,Name,Description,ConnectionString")] SnowflakeConnectionString snowflakeConnectionString)
        {
            string auditUser = ResolveAuditUser();

            if (snowflakeConnectionString.Id == 0)
            {
                snowflakeConnectionString.CreatedBy = auditUser;
                snowflakeConnectionString.CreatedAt = DateTimeHelper.GetCurrentLocalTime();
            }
            else
            {
                SnowflakeConnectionString? existing = await _service.GetByIdAsync(snowflakeConnectionString.Id);
                if (existing is null)
                {
                    return NotFound();
                }

                snowflakeConnectionString.CreatedBy = existing.CreatedBy;
                snowflakeConnectionString.CreatedAt = existing.CreatedAt;
                snowflakeConnectionString.ModifiedBy = auditUser;
            }

            ModelState.Remove(nameof(SnowflakeConnectionString.CreatedBy));
            ModelState.Remove(nameof(SnowflakeConnectionString.CreatedAt));
            ModelState.Remove(nameof(SnowflakeConnectionString.ModifiedBy));

            if (!ModelState.IsValid)
            {
                return View("Form", snowflakeConnectionString);
            }

            try
            {
                if (snowflakeConnectionString.Id == 0)
                {
                    await _service.CreateAsync(snowflakeConnectionString);
                    TempData["Success"] = "Snowflake connection string created successfully.";
                }
                else
                {
                    await _service.UpdateAsync(snowflakeConnectionString);
                    TempData["Success"] = "Snowflake connection string updated successfully.";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving snowflake connection string");
                TempData["Error"] = "An error occurred while saving the snowflake connection string.";
                return View("Form", snowflakeConnectionString);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _service.DeleteAsync(id);
                TempData["Success"] = "Snowflake connection string deleted successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting snowflake connection string");
                TempData["Error"] = "An error occurred while deleting the snowflake connection string.";
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