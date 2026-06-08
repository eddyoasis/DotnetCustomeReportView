using DataWarehousePower.Models;
using DataWarehousePower.Services;
using Microsoft.AspNetCore.Mvc;

namespace DataWarehousePower.Controllers
{
    public class ReportStaffController : Controller
    {
        private readonly IReportStaffService      _staffService;
        private readonly IColumnPreferenceService _prefService;
        private readonly ILogger<ReportStaffController> _logger;

        public ReportStaffController(
            IReportStaffService staffService,
            IColumnPreferenceService prefService,
            ILogger<ReportStaffController> logger)
        {
            _staffService = staffService;
            _prefService  = prefService;
            _logger       = logger;
        }

        // GET /ReportStaff
        public async Task<IActionResult> Index()
        {
            var userId      = _prefService.ResolveUserId(HttpContext);
            var systemCols  = _staffService.GetAvailableColumns();
            var displayCols = await _prefService.LoadPreferencesAsync(userId, systemCols);
            var data        = await _staffService.GetAllStaffAsync();

            var vm = new ReportStaffViewModel
            {
                AvailableColumns = systemCols.ToList(),
                DisplayColumns   = displayCols,
                Data             = data.ToList()
            };

            return View(vm);
        }

        // POST /ReportStaff/SavePreferences  (AJAX)
        [HttpPost]
        public async Task<IActionResult> SavePreferences([FromBody] SavePreferencesRequest? request)
        {
            if (request?.Columns == null || request.Columns.Count == 0)
                return BadRequest(new { success = false, error = "No columns supplied." });

            var userId     = _prefService.ResolveUserId(HttpContext);
            var systemCols = _staffService.GetAvailableColumns();

            try
            {
                await _prefService.SavePreferencesAsync(userId, request.Columns, systemCols);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save column preferences for user {UserId}", userId);
                return StatusCode(500, new { success = false, error = "Failed to save preferences." });
            }
        }
    }

    public class SavePreferencesRequest
    {
        public List<SaveColumnRequest> Columns { get; set; } = new();
    }
}
