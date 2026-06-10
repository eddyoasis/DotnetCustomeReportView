using DataWarehousePower.Models;
using DataWarehousePower.Services;
using Microsoft.AspNetCore.Mvc;

namespace DataWarehousePower.Controllers
{
    /// <summary>
    /// Generic report controller — serves any report defined in ReportDefinitions table.
    /// </summary>
    public class ReportController : Controller
    {
        private readonly IReportService          _reportService;
        private readonly IColumnPreferenceService _prefService;
        private readonly ILogger<ReportController> _logger;

        public ReportController(
            IReportService reportService,
            IColumnPreferenceService prefService,
            ILogger<ReportController> logger)
        {
            _reportService = reportService;
            _prefService   = prefService;
            _logger        = logger;
        }

        // GET /Report/{id}
        public async Task<IActionResult> Index(
            int id,
            string? clientCode = null,
            string? filterClientCode = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null)
        {
            var userId = _prefService.ResolveUserId(HttpContext);
            var vm     = await _reportService.BuildReportViewModelAsync(id, userId, clientCode, filterClientCode, dateFrom, dateTo);

            if (vm is null)
                return NotFound($"Report with ID {id} was not found.");

            return View(vm);
        }

        // GET /Report/List → redirect to first available report
        public async Task<IActionResult> List(
            string? clientCode = null,
            string? filterClientCode = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null)
        {
            var reports = await _reportService.GetAllReportsAsync();
            if (reports.Count == 0)
                return View("NoReports");

            return RedirectToAction(nameof(Index), new { id = reports[0].Id, clientCode, filterClientCode, dateFrom, dateTo });
        }

        // POST /Report/{id}/SavePreferences  (AJAX)
        [HttpPost]
        public async Task<IActionResult> SavePreferences(int id,
            [FromBody] SavePreferencesRequest? request)
        {
            if (request?.Columns == null || request.Columns.Count == 0)
                return BadRequest(new { success = false, error = "No columns supplied." });

            var userId = _prefService.ResolveUserId(HttpContext);

            // Load the system columns for this report to validate against
            var vm = await _reportService.BuildReportViewModelAsync(id, userId, request?.ClientCode);
            if (vm is null)
                return NotFound(new { success = false, error = "Report not found." });

            try
            {
                await _prefService.SavePreferencesAsync(userId, id, request.ClientCode, request.Columns, vm.AvailableColumns);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save preferences for user {UserId} report {ReportId} client code {ClientCode}", userId, id, request?.ClientCode);
                return StatusCode(500, new { success = false, error = "Failed to save preferences." });
            }
        }
    }

    public class SavePreferencesRequest
    {
        public string? ClientCode { get; set; }
        public List<SaveColumnRequest> Columns { get; set; } = new();
    }
}
