using DataWarehousePower.Authorization;
using DataWarehousePower.Helper;
using DataWarehousePower.Models;
using DataWarehousePower.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DataWarehousePower.Controllers
{
    [Authorize(Policy = DepartmentAuthorizationPolicies.ReportAccess)]
    public class DataFileController : Controller
    {
        private readonly IDataFileManageService _service;
        private readonly IScheduledReportJobService _scheduledReportJobService;
        private readonly IColumnPreferenceService _prefService;
        private readonly IDepartmentService _departmentService;
        private readonly IReportExportService _exportService;
        private readonly ILogger<DataFileController> _logger;

        public DataFileController(
            IDataFileManageService service,
            IScheduledReportJobService scheduledReportJobService,
            IColumnPreferenceService prefService,
            IDepartmentService departmentService,
            IReportExportService exportService,
            ILogger<DataFileController> logger)
        {
            _service = service;
            _scheduledReportJobService = scheduledReportJobService;
            _prefService = prefService;
            _departmentService = departmentService;
            _exportService = exportService;
            _logger = logger;
        }

        public async Task<IActionResult> Index(int? id = null, string? search = null, bool? isActive = null, string? schemaTemplate = null, DateTime? dateFrom = null, DateTime? dateTo = null)
        {
            string userId = _prefService.ResolveUserId(HttpContext);
            string? userDepartment = ResolveUserDepartment();

            DataFileManageFilterViewModel filter = new()
            {
                Search = search,
                IsActive = isActive
            };

            DataFileManageListViewModel listViewModel = await _service.GetListViewModelAsync(userId, userDepartment ?? string.Empty, filter);
            if (listViewModel.DataFiles.Count == 0)
            {
                return View("NoDataFiles");
            }

            DataFileDefinition selectedDataFile = id.HasValue
                ? listViewModel.DataFiles.FirstOrDefault(dataFile => dataFile.Id == id.Value) ?? listViewModel.DataFiles[0]
                : listViewModel.DataFiles[0];

            List<ColumnDefinition> availableColumns = selectedDataFile.Columns
                .OrderBy(column => column.DisplayOrder)
                .Select((column, index) => new ColumnDefinition
                {
                    Key = column.PropertyName,
                    DefaultLabel = column.DefaultLabel,
                    DisplayLabel = column.DefaultLabel,
                    IsVisible = true,
                    Order = column.DisplayOrder > 0 ? column.DisplayOrder : index + 1
                })
                .ToList();

            string normalizedSchemaTemplate = schemaTemplate?.Trim() ?? string.Empty;
            (List<ColumnDefinition> displayColumns, int? activePreferenceId) = await _prefService.LoadDataFileColumnPreferencesAsync(
                userId,
                selectedDataFile.Id,
                normalizedSchemaTemplate,
                availableColumns);

            List<string> savedSchemaTemplates = await _prefService.GetDataFileSchemaTemplatesAsync(userId, selectedDataFile.Id);
            Dictionary<string, int> schemaTemplatePreferenceIds = await _prefService.GetDataFileSchemaTemplatePreferenceIdsAsync(userId, selectedDataFile.Id);
            List<string> availableSchemaTemplates = BuildAvailableSchemaTemplates(normalizedSchemaTemplate, savedSchemaTemplates);

            ViewData["DepartmentLookup"] = await GetDepartmentLookupAsync();

            return View(new DataFileBrowserViewModel
            {
                Filter = listViewModel.Filter,
                DataFiles = listViewModel.DataFiles,
                SelectedDataFile = selectedDataFile,
                FilterDateFrom = dateFrom,
                FilterDateTo = dateTo,
                SchemaTemplate = normalizedSchemaTemplate,
                ActivePreferenceId = activePreferenceId,
                AvailableSchemaTemplates = availableSchemaTemplates,
                SchemaTemplatePreferenceIds = schemaTemplatePreferenceIds,
                AvailableColumns = availableColumns,
                DisplayColumns = displayColumns
            });
        }

        public async Task<IActionResult> List(string? search = null, bool? isActive = null, DateTime? dateFrom = null, DateTime? dateTo = null)
        {
            string userId = _prefService.ResolveUserId(HttpContext);
            string? userDepartment = ResolveUserDepartment();

            DataFileManageListViewModel listViewModel = await _service.GetListViewModelAsync(
                userId,
                userDepartment ?? string.Empty,
                new DataFileManageFilterViewModel
                {
                    Search = search,
                    IsActive = isActive
                });

            if (listViewModel.DataFiles.Count == 0)
            {
                return View("NoDataFiles");
            }

            return RedirectToAction(nameof(Index), new
            {
                id = listViewModel.DataFiles[0].Id,
                search,
                isActive,
                dateFrom,
                dateTo
            });
        }

        [HttpGet]
        public async Task<IActionResult> ScheduleDataFile(int id, string? schemaTemplate = null, string? returnUrl = null)
        {
            string userId = _prefService.ResolveUserId(HttpContext);
            int? existingJobId = await _scheduledReportJobService.FindExistingJobIdAsync(userId, id, schemaTemplate, null);

            if (existingJobId.HasValue)
            {
                return RedirectToAction("Edit", "ScheduledJob", new { id = existingJobId.Value });
            }

            return RedirectToAction("Create", "ScheduledJob", new
            {
                reportDefinitionId = id,
                schemaTemplate,
                clientCode = (string?)null,
                returnUrl,
                isDataFile = true
            });
        }

        [HttpPost]
        public async Task<IActionResult> SavePreferences(int id, [FromBody] SavePreferencesRequest? request)
        {
            if (request is null || request.Columns == null || request.Columns.Count == 0)
            {
                return BadRequest(new { success = false, error = "No columns supplied." });
            }

            string userId = _prefService.ResolveUserId(HttpContext);
            DataFileManageFormViewModel form = await _service.GetFormViewModelAsync(id);
            List<ColumnDefinition> systemColumns = form.Columns
                .Where(column => !column.IsDeleted)
                .OrderBy(column => column.DisplayOrder)
                .Select((column, index) => new ColumnDefinition
                {
                    Key = column.PropertyName,
                    DefaultLabel = column.DefaultLabel,
                    DisplayLabel = column.DefaultLabel,
                    IsVisible = true,
                    Order = column.DisplayOrder > 0 ? column.DisplayOrder : index + 1
                })
                .ToList();

            try
            {
                int preferenceId = await _prefService.SaveDataFilePreferencesAsync(userId, id, request.SchemaTemplate, request.PreferenceId, request.Columns, systemColumns);
                return Ok(new { success = true, preferenceId });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { success = false, error = "Failed to save preferences." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateSchemaTemplate(int id, [FromBody] UpdateSchemaTemplateRequest? request)
        {
            if (request is null || request.PreferenceId <= 0)
            {
                return BadRequest(new { success = false, error = "Valid preference id is required." });
            }

            if (string.IsNullOrWhiteSpace(request.NewSchemaTemplate))
            {
                return BadRequest(new { success = false, error = "New schema template cannot be blank." });
            }

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
            catch (Exception)
            {
                return StatusCode(500, new { success = false, error = "Failed to update schema template scope." });
            }
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteSchemaTemplate(int id, [FromBody] DeleteSchemaTemplateRequest? request)
        {
            if (request is null || request.PreferenceId <= 0)
            {
                return BadRequest(new { success = false, error = "Valid preference id is required." });
            }

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
            catch (Exception)
            {
                return StatusCode(500, new { success = false, error = "Failed to delete schema template scope." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Export(int id, [FromBody] ExportReportRequest? request, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                return BadRequest(new { success = false, error = "Export request is required." });
            }

            if (request.DateTo.HasValue)
                request.DateTo = request.DateTo.Value.AddDays(1).AddSeconds(-1);

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
                return BadRequest(new { success = false, error = "At least one format is required. Use CSV, Excel, or PDF." });
            }

            if (normalizedFormats.Any(format => format is not ("csv" or "excel" or "pdf")))
            {
                return BadRequest(new { success = false, error = "Invalid format selection. Use CSV, Excel, or PDF." });
            }

            string? passwordValidationError = ValidatePasswordStrength(request.Password);
            if (passwordValidationError is not null)
            {
                return BadRequest(new { success = false, error = passwordValidationError });
            }

            DataFileManageFormViewModel form;
            try
            {
                form = await _service.GetFormViewModelAsync(id);
            }
            catch (InvalidOperationException)
            {
                return NotFound(new { success = false, error = "Data file not found." });
            }

            if (!string.IsNullOrWhiteSpace(form.SourceSP))
            {
                return BadRequest(new { success = false, error = "Export currently supports Source Table only for data files." });
            }

            List<ColumnDefinition> exportColumns = form.Columns
                .Where(column => !column.IsDeleted)
                .OrderBy(column => column.DisplayOrder)
                .Select((column, index) => new ColumnDefinition
                {
                    Key = column.PropertyName,
                    DefaultLabel = column.DefaultLabel,
                    DisplayLabel = column.DefaultLabel,
                    IsVisible = true,
                    Order = column.DisplayOrder > 0 ? column.DisplayOrder : index + 1
                })
                .ToList();

            string userId = _prefService.ResolveUserId(HttpContext);
            string schemaTemplate = request.SchemaTemplate?.Trim() ?? string.Empty;
            (List<ColumnDefinition> displayColumns, _) = await _prefService.LoadDataFileColumnPreferencesAsync(
                userId,
                id,
                schemaTemplate,
                exportColumns);

            try
            {
                DataFilePreviewResult preview = await _service.GetPreviewDataAsync(new DataFilePreviewRequest
                {
                    SourceDatabase = form.SourceDatabase,
                    SourceTable = form.SourceTable,
                    SourceSP = form.SourceSP,
                    DateFrom = request.DateFrom,
                    DateTo = request.DateTo,
                    Columns = form.Columns
                        .Where(column => !column.IsDeleted)
                        .OrderBy(column => column.DisplayOrder)
                        .Select(column => new DataFilePreviewColumnRequest
                        {
                            PropertyName = column.PropertyName,
                            MappingParameter = column.MappingParameter,
                            MappingParameterFilter = column.MappingParameterFilter
                        })
                        .ToList(),
                    IsExport = true
                });

                var vm = new ReportViewModel
                {
                    ReportId = id,
                    ReportName = form.DataFileName,
                    FilterDateFrom = request.DateFrom,
                    FilterDateTo = request.DateTo,
                    AvailableColumns = exportColumns,
                    DisplayColumns = displayColumns,
                    Rows = preview.Rows
                };

                var dataFileDate = request.DateFrom == request.DateTo ?
                  $"{request.DateFrom:yyyy-MM-dd}" :
                  $"{request.DateFrom:yyyy-MM-dd}_{request.DateTo:yyyy-MM-dd}";

                string dataFileName = string.Join("_", vm.ReportName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
                string zipFileName = $"{dataFileName}_{request.ClientCode}_{dataFileDate}_({DateTimeHelper.GetCurrentLocalTime():yyyy-MM-dd_HHmm}).zip";
                string zipSubFileName = $"{dataFileName}_format_{request.ClientCode}_{dataFileDate}_({DateTimeHelper.GetCurrentLocalTime():yyyy-MM-dd_HHmm})";

                byte[] zipBytes = await _exportService.BuildPasswordProtectedZipAsync(
                    vm,
                    normalizedFormats,
                    request.Password,
                    zipSubFileName,
                    null,
                    cancellationToken);

                return File(zipBytes, "application/zip", zipFileName);
            }
            catch (ArgumentException argumentException)
            {
                _logger.LogWarning(argumentException, "Invalid data file export request for data file {DataFileId}", id);
                return BadRequest(new { success = false, error = argumentException.Message });
            }
            catch (InvalidOperationException invalidOperationException)
            {
                _logger.LogWarning(invalidOperationException, "Data file export validation failed for data file {DataFileId}", id);
                return BadRequest(new { success = false, error = invalidOperationException.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export data file {DataFileId}", id);
                return StatusCode(500, new { success = false, error = "Failed to export data file." });
            }
        }

        private async Task<Dictionary<int, string>> GetDepartmentLookupAsync()
        {
            return (await _departmentService.GetAllAsync())
                .GroupBy(department => department.Id)
                .ToDictionary(group => group.Key, group => group.First().Name);
        }

        private static List<string> BuildAvailableSchemaTemplates(string currentSchemaTemplate, IEnumerable<string> savedSchemaTemplates)
            => savedSchemaTemplates
                .Append(currentSchemaTemplate)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();

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

        private string? ResolveUserDepartment()
            => HttpContext.Session.GetString("UserDepartment");
    }
}