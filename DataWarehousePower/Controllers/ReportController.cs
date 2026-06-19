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
        private readonly IScheduledReportJobService _scheduledReportJobService;
        private readonly IAuditLogService _auditLogService;
        private readonly ILogger<ReportController> _logger;

        public ReportController(
            IReportService reportService,
            IColumnPreferenceService prefService,
            IReportExportService exportService,
            IScheduledReportJobService scheduledReportJobService,
            IAuditLogService auditLogService,
            ILogger<ReportController> logger)
        {
            _reportService = reportService;
            _prefService   = prefService;
            _exportService = exportService;
            _scheduledReportJobService = scheduledReportJobService;
            _auditLogService = auditLogService;
            _logger        = logger;
        }

        // GET /Report/{id}
        public async Task<IActionResult> Index(
            int id,
            string? schemaTemplate = null,
            string? clientCode = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null)
        {
            var userId = _prefService.ResolveUserId(HttpContext);
            var vm     = await _reportService.BuildReportViewModelAsync(id, userId, schemaTemplate, clientCode, dateFrom, dateTo);

            if (vm is null)
                return NotFound($"Report with ID {id} was not found.");

            return View(vm);
        }

        // GET /Report/List → redirect to first available report
        public async Task<IActionResult> List(
            string? schemaTemplate = null,
            string? clientCode = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null)
        {
            var reports = await _reportService.GetAllReportsAsync();
            if (reports.Count == 0)
                return View("NoReports");

            return RedirectToAction(nameof(Index), new { id = reports[0].Id, schemaTemplate, clientCode, dateFrom, dateTo });
        }

        [HttpGet]
        public async Task<IActionResult> ScheduleReport(int id, string? schemaTemplate = null, string? clientCode = null, List<string>? formats = null)
        {
            string userId = _prefService.ResolveUserId(HttpContext);
            int? existingJobId = await _scheduledReportJobService.FindExistingJobIdAsync(userId, id, schemaTemplate, clientCode);

            if (existingJobId.HasValue)
            {
                return RedirectToAction("Edit", "ScheduledJob", new { id = existingJobId.Value });
            }

            return RedirectToAction("Create", "ScheduledJob", new
            {
                reportDefinitionId = id,
                schemaTemplate,
                clientCode,
                formats
            });
        }

        // POST /Report/{id}/SavePreferences  (AJAX)
        [HttpPost]
        public async Task<IActionResult> SavePreferences(int id,
            [FromBody] SavePreferencesRequest? request)
        {
            if (request is null || request.Columns == null || request.Columns.Count == 0)
                return BadRequest(new { success = false, error = "No columns supplied." });

            SavePreferencesRequest requestModel = request;

            var userId = _prefService.ResolveUserId(HttpContext);

            // Load the system columns for this report to validate against
            var vm = await _reportService.BuildReportViewModelAsync(id, userId, requestModel.SchemaTemplate);
            if (vm is null)
                return NotFound(new { success = false, error = "Report not found." });

            try
            {
                int preferenceId = await _prefService.SavePreferencesAsync(userId, id, requestModel.SchemaTemplate, requestModel.PreferenceId, requestModel.Columns, vm.AvailableColumns);
                return Ok(new { success = true, preferenceId });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save preferences for user {UserId} report {ReportId} schema template {SchemaTemplate}", userId, id, requestModel.SchemaTemplate);
                return StatusCode(500, new { success = false, error = "Failed to save preferences." });
            }
        }

        // POST /Report/{id}/UpdateSchemaTemplate  (AJAX)
        [HttpPost]
        public async Task<IActionResult> UpdateSchemaTemplate(int id, [FromBody] UpdateSchemaTemplateRequest? request)
        {
            if (request is null || request.PreferenceId <= 0)
                return BadRequest(new { success = false, error = "Valid preference id is required." });

            if (string.IsNullOrWhiteSpace(request.NewSchemaTemplate))
                return BadRequest(new { success = false, error = "New schema template cannot be blank." });

            string userId = _prefService.ResolveUserId(HttpContext);

            try
            {
                await _prefService.UpdateSchemaTemplateAsync(userId, id, request.PreferenceId, request.NewSchemaTemplate);
                return Ok(new { success = true, preferenceId = request.PreferenceId, schemaTemplate = request.NewSchemaTemplate.Trim() });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update preference scope for user {UserId} report {ReportId} preference {PreferenceId}", userId, id, request.PreferenceId);
                return StatusCode(500, new { success = false, error = "Failed to update schema template scope." });
            }
        }

        // DELETE /Report/{id}/DeleteSchemaTemplate  (AJAX)
        [HttpDelete]
        public async Task<IActionResult> DeleteSchemaTemplate(int id, [FromBody] DeleteSchemaTemplateRequest? request)
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
                return StatusCode(500, new { success = false, error = "Failed to delete schema template scope." });
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

            List<string> normalizedFormats = (request.Formats ?? [])
                .Where(format => !string.IsNullOrWhiteSpace(format))
                .Select(format => format.Trim().ToLowerInvariant())
                .Distinct()
                .ToList();

            if (normalizedFormats.Count == 0 && !string.IsNullOrWhiteSpace(request.Format))
            {
                normalizedFormats.Add(request.Format.Trim().ToLowerInvariant());
            }

            if (normalizedFormats.Count == 0)
            {
                await _auditLogService.LogExportAsync("ExportRejected", userId, username, correlationId, id, null, "unknown", request.SchemaTemplate, request.ClientCode, request.DateFrom, request.DateTo, "At least one format must be selected.", cancellationToken);
                return BadRequest(new { success = false, error = "At least one format is required. Use CSV, Excel, or PDF." });
            }

            bool hasInvalidFormat = normalizedFormats.Any(format => format is not ("csv" or "excel" or "pdf"));
            string normalizedFormatsAuditValue = string.Join(",", normalizedFormats);
            if (hasInvalidFormat)
            {
                await _auditLogService.LogExportAsync("ExportRejected", userId, username, correlationId, id, null, normalizedFormatsAuditValue, request.SchemaTemplate, request.ClientCode, request.DateFrom, request.DateTo, "Invalid format.", cancellationToken);
                return BadRequest(new { success = false, error = "Invalid format selection. Use CSV, Excel, or PDF." });
            }

            string? passwordValidationError = ValidatePasswordStrength(request.Password);
            if (passwordValidationError is not null)
            {
                await _auditLogService.LogExportAsync("ExportRejected", userId, username, correlationId, id, null, normalizedFormatsAuditValue, request.SchemaTemplate, request.ClientCode, request.DateFrom, request.DateTo, passwordValidationError, cancellationToken);
                return BadRequest(new { success = false, error = passwordValidationError });
            }

            ReportViewModel? vm = await _reportService.BuildReportViewModelAsync(
                id,
                userId,
                request.SchemaTemplate,
                request.ClientCode,
                request.DateFrom,
                request.DateTo);

            if (vm is null)
            {
                await _auditLogService.LogExportAsync("ExportRejected", userId, username, correlationId, id, null, normalizedFormatsAuditValue, request.SchemaTemplate, request.ClientCode, request.DateFrom, request.DateTo, "Report not found.", cancellationToken);
                return NotFound(new { success = false, error = "Report not found." });
            }

            try
            {
                byte[] zipBytes = await _exportService.BuildPasswordProtectedZipAsync(
                    vm,
                    normalizedFormats,
                    request.Password,
                    cancellationToken);

                string reportName = string.Join("_", vm.ReportName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
                string zipFileName = $"{reportName}_export.zip";

                await _auditLogService.LogExportAsync("ExportSucceeded", userId, username, correlationId, id, vm.ReportName, normalizedFormatsAuditValue, request.SchemaTemplate, request.ClientCode, request.DateFrom, request.DateTo, "ZIP generated and returned.", cancellationToken);

                return File(zipBytes, "application/zip", zipFileName);
            }
            catch (ArgumentException argumentException)
            {
                _logger.LogWarning(argumentException, "Invalid export request for report {ReportId}", id);
                await _auditLogService.LogExportAsync("ExportFailed", userId, username, correlationId, id, vm.ReportName, normalizedFormatsAuditValue, request.SchemaTemplate, request.ClientCode, request.DateFrom, request.DateTo, argumentException.Message, cancellationToken);
                return BadRequest(new { success = false, error = argumentException.Message });
            }
            catch (InvalidOperationException invalidOperationException)
            {
                _logger.LogWarning(invalidOperationException, "Export validation failed for report {ReportId}", id);
                await _auditLogService.LogExportAsync("ExportFailed", userId, username, correlationId, id, vm.ReportName, normalizedFormatsAuditValue, request.SchemaTemplate, request.ClientCode, request.DateFrom, request.DateTo, invalidOperationException.Message, cancellationToken);
                return BadRequest(new { success = false, error = invalidOperationException.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export report {ReportId}", id);
                await _auditLogService.LogExportAsync("ExportFailed", userId, username, correlationId, id, vm.ReportName, normalizedFormatsAuditValue, request.SchemaTemplate, request.ClientCode, request.DateFrom, request.DateTo, "Unexpected export error.", cancellationToken);
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
        public string? SchemaTemplate { get; set; }
        public int? PreferenceId { get; set; }
        public List<SaveColumnRequest> Columns { get; set; } = new();
    }

    public class UpdateSchemaTemplateRequest
    {
        public int PreferenceId { get; set; }
        public string NewSchemaTemplate { get; set; } = string.Empty;
    }

    public class DeleteSchemaTemplateRequest
    {
        public int PreferenceId { get; set; }
    }
}
