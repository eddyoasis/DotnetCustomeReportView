using DataWarehousePower.Authorization;
using DataWarehousePower.Models;
using DataWarehousePower.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DataWarehousePower.Controllers
{
    /// <summary>
    /// Generic report controller — serves any report defined in ReportDefinitions table.
    /// </summary>
    [Authorize(Policy = DepartmentAuthorizationPolicies.ReportAccess)]
    public class ReportController : Controller
    {
        private readonly IReportService          _reportService;
        private readonly IColumnPreferenceService _prefService;
        private readonly IReportExportService _exportService;
        private readonly IAuditLogService _auditLogService;
        private readonly ILogger<ReportController> _logger;

        public ReportController(
            IReportService reportService,
            IColumnPreferenceService prefService,
            IReportExportService exportService,
            IAuditLogService auditLogService,
            ILogger<ReportController> logger)
        {
            _reportService = reportService;
            _prefService   = prefService;
            _exportService = exportService;
            _auditLogService = auditLogService;
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
                int preferenceId = await _prefService.SavePreferencesAsync(userId, id, request.ClientCode, request.PreferenceId, request.Columns, vm.AvailableColumns);
                return Ok(new { success = true, preferenceId });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save preferences for user {UserId} report {ReportId} client code {ClientCode}", userId, id, request?.ClientCode);
                return StatusCode(500, new { success = false, error = "Failed to save preferences." });
            }
        }

        // POST /Report/{id}/UpdateClientCode  (AJAX)
        [HttpPost]
        public async Task<IActionResult> UpdateClientCode(int id, [FromBody] UpdateClientCodeRequest? request)
        {
            if (request is null || request.PreferenceId <= 0)
                return BadRequest(new { success = false, error = "Valid preference id is required." });

            if (string.IsNullOrWhiteSpace(request.NewClientCode))
                return BadRequest(new { success = false, error = "New client code cannot be blank." });

            string userId = _prefService.ResolveUserId(HttpContext);

            try
            {
                await _prefService.UpdateClientCodeAsync(userId, id, request.PreferenceId, request.NewClientCode);
                return Ok(new { success = true, preferenceId = request.PreferenceId, clientCode = request.NewClientCode.Trim() });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update preference scope for user {UserId} report {ReportId} preference {PreferenceId}", userId, id, request.PreferenceId);
                return StatusCode(500, new { success = false, error = "Failed to update client code scope." });
            }
        }

        // DELETE /Report/{id}/DeleteClientCode  (AJAX)
        [HttpDelete]
        public async Task<IActionResult> DeleteClientCode(int id, [FromBody] DeleteClientCodeRequest? request)
        {
            if (request is null || request.PreferenceId <= 0)
                return BadRequest(new { success = false, error = "Valid preference id is required." });

            string userId = _prefService.ResolveUserId(HttpContext);

            try
            {
                await _prefService.DeletePreferenceAsync(userId, id, request.PreferenceId);
                return Ok(new { success = true });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete preference scope for user {UserId} report {ReportId} preference {PreferenceId}", userId, id, request.PreferenceId);
                return StatusCode(500, new { success = false, error = "Failed to delete client code scope." });
            }
        }

        // POST /Report/{id}/Export
        [HttpPost]
        public async Task<IActionResult> Export(int id, [FromBody] ExportReportRequest? request, CancellationToken cancellationToken)
        {
            string userId = _prefService.ResolveUserId(HttpContext);
            string username = GetAuditUsername();
            string correlationId = HttpContext.TraceIdentifier;

            if (request is null)
            {
                await _auditLogService.LogExportAsync("ExportRejected", userId, username, correlationId, id, null, "unknown", null, null, null, null, "Request body is missing.", cancellationToken);
                return BadRequest(new { success = false, error = "Export request is required." });
            }

            string normalizedFormat = request.Format?.Trim().ToLowerInvariant() ?? string.Empty;
            if (normalizedFormat is not ("csv" or "excel" or "pdf"))
            {
                await _auditLogService.LogExportAsync("ExportRejected", userId, username, correlationId, id, null, normalizedFormat, request.ClientCode, request.FilterClientCode, request.DateFrom, request.DateTo, "Invalid format.", cancellationToken);
                return BadRequest(new { success = false, error = "Invalid format. Use CSV, Excel, or PDF." });
            }

            string? passwordValidationError = ValidatePasswordStrength(request.Password);
            if (passwordValidationError is not null)
            {
                await _auditLogService.LogExportAsync("ExportRejected", userId, username, correlationId, id, null, normalizedFormat, request.ClientCode, request.FilterClientCode, request.DateFrom, request.DateTo, passwordValidationError, cancellationToken);
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
                await _auditLogService.LogExportAsync("ExportRejected", userId, username, correlationId, id, null, normalizedFormat, request.ClientCode, request.FilterClientCode, request.DateFrom, request.DateTo, "Report not found.", cancellationToken);
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

                await _auditLogService.LogExportAsync("ExportSucceeded", userId, username, correlationId, id, vm.ReportName, normalizedFormat, request.ClientCode, request.FilterClientCode, request.DateFrom, request.DateTo, "ZIP generated and returned.", cancellationToken);

                return File(zipBytes, "application/zip", zipFileName);
            }
            catch (ArgumentException argumentException)
            {
                _logger.LogWarning(argumentException, "Invalid export request for report {ReportId}", id);
                await _auditLogService.LogExportAsync("ExportFailed", userId, username, correlationId, id, vm.ReportName, normalizedFormat, request.ClientCode, request.FilterClientCode, request.DateFrom, request.DateTo, argumentException.Message, cancellationToken);
                return BadRequest(new { success = false, error = argumentException.Message });
            }
            catch (InvalidOperationException invalidOperationException)
            {
                _logger.LogWarning(invalidOperationException, "Export validation failed for report {ReportId}", id);
                await _auditLogService.LogExportAsync("ExportFailed", userId, username, correlationId, id, vm.ReportName, normalizedFormat, request.ClientCode, request.FilterClientCode, request.DateFrom, request.DateTo, invalidOperationException.Message, cancellationToken);
                return BadRequest(new { success = false, error = invalidOperationException.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export report {ReportId}", id);
                await _auditLogService.LogExportAsync("ExportFailed", userId, username, correlationId, id, vm.ReportName, normalizedFormat, request.ClientCode, request.FilterClientCode, request.DateFrom, request.DateTo, "Unexpected export error.", cancellationToken);
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

        private string GetAuditUsername()
        {
            string? displayName = HttpContext.Session.GetString("UserDisplayName");
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                return displayName.Trim();
            }

            return string.IsNullOrWhiteSpace(User.Identity?.Name) ? "Anonymous" : User.Identity!.Name!.Trim();
        }
    }

    public class SavePreferencesRequest
    {
        public string? ClientCode { get; set; }
        public int? PreferenceId { get; set; }
        public List<SaveColumnRequest> Columns { get; set; } = new();
    }

    public class UpdateClientCodeRequest
    {
        public int PreferenceId { get; set; }
        public string NewClientCode { get; set; } = string.Empty;
    }

    public class DeleteClientCodeRequest
    {
        public int PreferenceId { get; set; }
    }
}
