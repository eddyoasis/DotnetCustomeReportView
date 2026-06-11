using DataWarehousePower.Models;
using DataWarehousePower.Services;
using log4net;
using Microsoft.AspNetCore.Mvc;

namespace DataWarehousePower.Controllers
{
    /// <summary>
    /// Generic report controller — serves any report defined in ReportDefinitions table.
    /// </summary>
    public class ReportController : Controller
    {
        private static readonly ILog AuditLog = LogManager.GetLogger("AuditLogger");

        private readonly IReportService          _reportService;
        private readonly IColumnPreferenceService _prefService;
        private readonly IReportExportService _exportService;
        private readonly ILogger<ReportController> _logger;

        public ReportController(
            IReportService reportService,
            IColumnPreferenceService prefService,
            IReportExportService exportService,
            ILogger<ReportController> logger)
        {
            _reportService = reportService;
            _prefService   = prefService;
            _exportService = exportService;
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

        // POST /Report/{id}/Export
        [HttpPost]
        public async Task<IActionResult> Export(int id, [FromBody] ExportReportRequest? request, CancellationToken cancellationToken)
        {
            string userId = _prefService.ResolveUserId(HttpContext);
            string correlationId = HttpContext.TraceIdentifier;

            if (request is null)
            {
                WriteAuditLog("ExportRejected", userId, correlationId, id, "unknown", string.Empty, "Request body is missing.");
                return BadRequest(new { success = false, error = "Export request is required." });
            }

            string normalizedFormat = request.Format?.Trim().ToLowerInvariant() ?? string.Empty;
            if (normalizedFormat is not ("csv" or "excel" or "pdf"))
            {
                WriteAuditLog("ExportRejected", userId, correlationId, id, normalizedFormat, request.ClientCode, "Invalid format.");
                return BadRequest(new { success = false, error = "Invalid format. Use CSV, Excel, or PDF." });
            }

            string? passwordValidationError = ValidatePasswordStrength(request.Password);
            if (passwordValidationError is not null)
            {
                WriteAuditLog("ExportRejected", userId, correlationId, id, normalizedFormat, request.ClientCode, passwordValidationError);
                return BadRequest(new { success = false, error = passwordValidationError });
            }

            ReportViewModel? vm = await _reportService.BuildReportViewModelAsync(
                id,
                userId,
                request.ClientCode,
                request.FilterClientCode,
                request.DateFrom,
                request.DateTo);

            if (vm is null)
            {
                WriteAuditLog("ExportRejected", userId, correlationId, id, normalizedFormat, request.ClientCode, "Report not found.");
                return NotFound(new { success = false, error = "Report not found." });
            }

            try
            {
                byte[] zipBytes = await _exportService.BuildPasswordProtectedZipAsync(
                    vm,
                    normalizedFormat,
                    request.Password,
                    cancellationToken);

                string reportName = string.Join("_", vm.ReportName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
                string zipFileName = $"{reportName}_export.zip";

                WriteAuditLog("ExportSucceeded", userId, correlationId, id, normalizedFormat, request.ClientCode, "ZIP generated and returned.");

                return File(zipBytes, "application/zip", zipFileName);
            }
            catch (ArgumentException argumentException)
            {
                _logger.LogWarning(argumentException, "Invalid export request for report {ReportId}", id);
                WriteAuditLog("ExportFailed", userId, correlationId, id, normalizedFormat, request.ClientCode, argumentException.Message);
                return BadRequest(new { success = false, error = argumentException.Message });
            }
            catch (InvalidOperationException invalidOperationException)
            {
                _logger.LogWarning(invalidOperationException, "Export validation failed for report {ReportId}", id);
                WriteAuditLog("ExportFailed", userId, correlationId, id, normalizedFormat, request.ClientCode, invalidOperationException.Message);
                return BadRequest(new { success = false, error = invalidOperationException.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export report {ReportId}", id);
                WriteAuditLog("ExportFailed", userId, correlationId, id, normalizedFormat, request.ClientCode, "Unexpected export error.");
                return StatusCode(500, new { success = false, error = "Failed to export report." });
            }
        }

        private static string? ValidatePasswordStrength(string? password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return "Password is required.";
            }

            string trimmed = password.Trim();
            if (trimmed.Length < 8)
            {
                return "Password must be at least 8 characters.";
            }

            bool hasUpper = trimmed.Any(char.IsUpper);
            bool hasLower = trimmed.Any(char.IsLower);
            bool hasDigit = trimmed.Any(char.IsDigit);
            bool hasSymbol = trimmed.Any(character => !char.IsLetterOrDigit(character));

            if (!hasUpper || !hasLower || !hasDigit || !hasSymbol)
            {
                return "Password must include uppercase, lowercase, number, and symbol.";
            }

            return null;
        }

        private static void WriteAuditLog(
            string action,
            string userId,
            string correlationId,
            int reportId,
            string? format,
            string? clientCode,
            string detail)
        {
            string normalizedFormat = string.IsNullOrWhiteSpace(format) ? "unknown" : format.Trim().ToLowerInvariant();
            string normalizedClientCode = string.IsNullOrWhiteSpace(clientCode) ? "Default" : clientCode.Trim();

            AuditLog.Info($"action={action};userId={userId};reportId={reportId};format={normalizedFormat};clientCode={normalizedClientCode};correlationId={correlationId};detail={detail}");
        }
    }

    public class SavePreferencesRequest
    {
        public string? ClientCode { get; set; }
        public List<SaveColumnRequest> Columns { get; set; } = new();
    }
}
