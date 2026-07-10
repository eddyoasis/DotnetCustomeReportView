using DataWarehousePower.Authorization;
using DataWarehousePower.Helper;
using DataWarehousePower.Models;
using DataWarehousePower.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DataWarehousePower.Controllers
{
    [Authorize(Policy = DepartmentAuthorizationPolicies.AdminAccess)]
    public class DWSchemeController : Controller
    {
        private readonly IDWSchemeService _service;
        private readonly ILogger<DWSchemeController> _logger;

        public DWSchemeController(
            IDWSchemeService service,
            ILogger<DWSchemeController> logger)
        {
            _service = service;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            List<DWScheme> dwSchemes = await _service.GetAllAsync();
            return View(dwSchemes);
        }

        public IActionResult Create()
        {
            return View("Form", new DWScheme());
        }

        public async Task<IActionResult> Edit(int id)
        {
            DWScheme? dwScheme = await _service.GetByIdAsync(id);
            if (dwScheme is null)
            {
                return NotFound();
            }

            return View("Form", dwScheme);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save([Bind("Id,SP,Display,FilterDateColumnName")] DWScheme dwScheme)
        {
            string auditUser = ResolveAuditUser();

            if (dwScheme.Id == 0)
            {
                dwScheme.InsertedBy = auditUser;
                dwScheme.InsertedDatetime = DateTimeHelper.GetCurrentLocalTime();
            }
            else
            {
                DWScheme? existing = await _service.GetByIdAsync(dwScheme.Id);
                if (existing is null)
                {
                    return NotFound();
                }

                dwScheme.InsertedBy = existing.InsertedBy;
                dwScheme.InsertedDatetime = existing.InsertedDatetime;
                dwScheme.ModifiedBy = auditUser;
            }

            ModelState.Remove(nameof(DWScheme.InsertedBy));
            ModelState.Remove(nameof(DWScheme.InsertedDatetime));
            ModelState.Remove(nameof(DWScheme.ModifiedBy));
            ModelState.Remove(nameof(DWScheme.ModifiedDatetime));

            if (!ModelState.IsValid)
            {
                return View("Form", dwScheme);
            }

            try
            {
                if (dwScheme.Id == 0)
                {
                    await _service.CreateAsync(dwScheme);
                    TempData["Success"] = "DW scheme created successfully.";
                }
                else
                {
                    await _service.UpdateAsync(dwScheme);
                    TempData["Success"] = "DW scheme updated successfully.";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving DW scheme");
                TempData["Error"] = "An error occurred while saving the DW scheme.";
                return View("Form", dwScheme);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _service.DeleteAsync(id);
                TempData["Success"] = "DW scheme deleted successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting DW scheme");
                TempData["Error"] = "An error occurred while deleting the DW scheme.";
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
