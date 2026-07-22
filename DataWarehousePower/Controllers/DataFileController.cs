using DataWarehousePower.Authorization;
using DataWarehousePower.Helper;
using DataWarehousePower.Models;
using DataWarehousePower.Models.AppSettings;
using DataWarehousePower.Repositories;
using DataWarehousePower.Services;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Snowflake.Data.Client;

namespace DataWarehousePower.Controllers
{
    [Authorize(Policy = DepartmentAuthorizationPolicies.DataFileAccess)]
    public class DataFileController : Controller
    {
        private readonly IDataFileManageService _service;
        private readonly IScheduledReportJobService _scheduledReportJobService;
        private readonly IColumnPreferenceService _prefService;
        private readonly IDepartmentService _departmentService;
        private readonly IReportExportService _exportService;
        private readonly IReportRepository _reportRepository;
        private readonly ISnowflakeService _snowflakeService;
        private readonly ILogger<DataFileController> _logger;
        private readonly ScheduledJob _scheduledJobAppSetting;

        public DataFileController(
            IOptionsSnapshot<ScheduledJob> scheduledJobAppSetting,
            IDataFileManageService service,
            IScheduledReportJobService scheduledReportJobService,
            IColumnPreferenceService prefService,
            IDepartmentService departmentService,
            IReportExportService exportService,
            IReportRepository reportRepository,
            ISnowflakeService snowflakeService,
            ILogger<DataFileController> logger)
        {
            _scheduledJobAppSetting = scheduledJobAppSetting.Value;
            _service = service;
            _scheduledReportJobService = scheduledReportJobService;
            _prefService = prefService;
            _departmentService = departmentService;
            _exportService = exportService;
            _reportRepository = reportRepository;
            _snowflakeService = snowflakeService;
            _logger = logger;
        }

        public async Task<IActionResult> Index(int? id = null, string? search = null, bool? isActive = null, string? schemaTemplate = null, string? clientCode = null, DateTime? dateFrom = null, DateTime? dateTo = null, Dictionary<string, string?>? columnFilters = null)
        {
            string userId = _prefService.ResolveUserId(HttpContext);
            string? userDepartment = ResolveUserDepartment();

            DataFileManageFilterViewModel filter = new()
            {
                Search = search,
                IsActive = isActive
            };

            //DataFileManageListViewModel listViewModel = await _service.GetListViewModelAsync(userId, filter);
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
                    Order = column.DisplayOrder > 0 ? column.DisplayOrder : index + 1,
                    MappingParameter = column.MappingParameter,
                    PropertyName = column.PropertyName,
                    PropertyType = column.PropertyType
                })
                .ToList();

            string normalizedSchemaTemplate = schemaTemplate?.Trim() ?? string.Empty;
            string normalizedClientCode = clientCode?.Trim() ?? string.Empty;
            (List<ColumnDefinition> displayColumns, int? activePreferenceId) = await _prefService.LoadDataFileColumnPreferencesAsync(
                userId,
                selectedDataFile.Id,
                normalizedSchemaTemplate,
                availableColumns);

            List<string> availableClientCodes = (await _reportRepository.GetClientCodesByUserIdAsync(userId))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!string.IsNullOrWhiteSpace(normalizedClientCode) &&
                !availableClientCodes.Contains(normalizedClientCode, StringComparer.OrdinalIgnoreCase))
            {
                availableClientCodes.Add(normalizedClientCode);
                availableClientCodes = availableClientCodes
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            List<string> savedSchemaTemplates = await _prefService.GetDataFileSchemaTemplatesAsync(userId, selectedDataFile.Id);
            Dictionary<string, int> schemaTemplatePreferenceIds = await _prefService.GetDataFileSchemaTemplatePreferenceIdsAsync(userId, selectedDataFile.Id);
            List<string> availableSchemaTemplates = BuildAvailableSchemaTemplates(normalizedSchemaTemplate, savedSchemaTemplates);
            Dictionary<string, string?> normalizedColumnFilters = (columnFilters ?? new Dictionary<string, string?>())
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
                .ToDictionary(
                    pair => pair.Key.Trim(),
                    pair => pair.Value?.Trim(),
                    StringComparer.OrdinalIgnoreCase);

            ViewData["DepartmentLookup"] = await GetDepartmentLookupAsync();

            return View(new DataFileBrowserViewModel
            {
                Filter = listViewModel.Filter,
                DataFiles = listViewModel.DataFiles.Any() ? listViewModel.DataFiles.Where(x=> string.IsNullOrEmpty(x.Departments)).ToList() : listViewModel.DataFiles,
                DefaultDataFiles = listViewModel.DataFiles.Any() ? listViewModel.DataFiles.Where(x => !string.IsNullOrEmpty(x.Departments)).ToList() : listViewModel.DataFiles,
                SelectedDataFile = selectedDataFile,
                ClientCode = normalizedClientCode,
                AvailableClientCodes = availableClientCodes,
                FilterDateFrom = dateFrom,
                FilterDateTo = dateTo,
                SchemaTemplate = normalizedSchemaTemplate,
                ActivePreferenceId = activePreferenceId,
                AvailableSchemaTemplates = availableSchemaTemplates,
                SchemaTemplatePreferenceIds = schemaTemplatePreferenceIds,
                AvailableColumns = availableColumns,
                DisplayColumns = displayColumns,
                ColumnFilters = normalizedColumnFilters,
                HasAppliedFilters = true,
                IsAllowEdit = selectedDataFile.UserId == userId
                //HasAppliedFilters = !string.IsNullOrEmpty(clientCode)
                //    || dateFrom.HasValue
                //    || dateTo.HasValue
                //    || normalizedColumnFilters.Count > 0
            });
        }

        public async Task<IActionResult> List(string? search = null, bool? isActive = null, string? clientCode = null, DateTime? dateFrom = null, DateTime? dateTo = null)
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
                clientCode,
                dateFrom,
                dateTo
            });
        }

        [HttpGet]
        public async Task<IActionResult> ScheduleDataFile(int id, string? schemaTemplate = null, string? clientCode = null, List<string>? formats = null, string? returnUrl = null)
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
                formats,
                returnUrl,
                isDataFile = true
            });
        }

        [HttpGet]
        public async Task<IActionResult> FilterValues2(int id, string? columnKey = null, string? search = null, int take = 50)
        {
            string userId = _prefService.ResolveUserId(HttpContext);
            string? userDepartment = ResolveUserDepartment();

            DataFileManageListViewModel listViewModel = await _service.GetListViewModelAsync(
                userId,
                userDepartment ?? string.Empty,
                new DataFileManageFilterViewModel());

            DataFileDefinition? selectedDataFile = listViewModel.DataFiles.FirstOrDefault(dataFile => dataFile.Id == id);
            if (selectedDataFile is null)
            {
                return Json(Array.Empty<string>());
            }

            string normalizedColumnKey = (columnKey ?? string.Empty).Trim();
            DataFileColumn? selectedColumn = selectedDataFile.Columns.FirstOrDefault(column =>
                !string.IsNullOrWhiteSpace(column.PropertyName) &&
                column.PropertyName.Equals(normalizedColumnKey, StringComparison.OrdinalIgnoreCase));

            if (selectedColumn is null)
            {
                return Json(Array.Empty<string>());
            }

            try
            {
                List<string> values = await _service.GetDistinctColumnValuesAsync(
                    selectedDataFile.SourceDatabase,
                    selectedDataFile.SourceTable,
                    selectedDataFile.SourceSP,
                    selectedColumn.PropertyName,
                    search,
                    take);

                return Json(values);
            }
            catch (SqlException ex) when (ex.Number is 916 or 229 or 911 or 11514)
            {
                _logger.LogWarning(ex,
                    "Column filter values query failed for data file {DataFileId}, database {SourceDatabase}, table {SourceTable}, column {ColumnName}",
                    id,
                    selectedDataFile.SourceDatabase,
                    selectedDataFile.SourceTable,
                    selectedColumn.PropertyName);

                return Json(Array.Empty<string>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> FilterValues(int id, string? columnKey = null, string? search = null, int take = 50)
        {
            string userId = _prefService.ResolveUserId(HttpContext);
            string? userDepartment = ResolveUserDepartment();

            DataFileManageListViewModel listViewModel = await _service.GetListViewModelAsync(
                userId,
                userDepartment ?? string.Empty,
                new DataFileManageFilterViewModel());

            DataFileDefinition? selectedDataFile = listViewModel.DataFiles.FirstOrDefault(dataFile => dataFile.Id == id);
            if (selectedDataFile is null)
            {
                return Json(Array.Empty<string>());
            }

            string normalizedColumnKey = (columnKey ?? string.Empty).Trim();
            DataFileColumn? selectedColumn = selectedDataFile.Columns.FirstOrDefault(column =>
                !string.IsNullOrWhiteSpace(column.PropertyName) &&
                column.PropertyName.Equals(normalizedColumnKey, StringComparison.OrdinalIgnoreCase));

            if (selectedColumn is null)
            {
                return Json(Array.Empty<string>());
            }

            try
            {
                List<string> values = _scheduledJobAppSetting.UseSnowflakeForDataFile ?
                    await _snowflakeService.GetDistinctColumnValuesAsync(
                    userDepartment,
                    selectedDataFile.SourceDatabase,
                    selectedDataFile.SourceTable,
                    selectedDataFile.SourceSP,
                    selectedColumn.PropertyName,
                    search,
                    take)
                    :
                    await _service.GetDistinctColumnValuesAsync(
                    selectedDataFile.SourceDatabase,
                    selectedDataFile.SourceTable,
                    selectedDataFile.SourceSP,
                    selectedColumn.PropertyName,
                    search,
                    take);

                return Json(values);
            }
            catch (SnowflakeDbException ex)
            {
                _logger.LogWarning(ex,
                    "Snowflake column filter values query failed for data file {DataFileId}, database {SourceDatabase}, table {SourceTable}, column {ColumnName}",
                    id,
                    selectedDataFile.SourceDatabase,
                    selectedDataFile.SourceTable,
                    selectedColumn.PropertyName);

                return Json(Array.Empty<string>());
            }
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
                DataFilePreviewResult preview = _scheduledJobAppSetting.UseSnowflakeForDataFile ? 
                    await _snowflakeService.GetPreviewDataAsync(new DataFilePreviewRequest
                    {
                        UserId = userId,
                        SourceDatabase = form.SourceDatabase,
                        SourceTable = form.SourceTable,
                        SourceSP = form.SourceSP,
                        ClientCode = request.ClientCode,
                        DateFrom = request.DateFrom,
                        DateTo = request.DateTo,
                        Parameters = request.Parameters,
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
                    })
                    :
                    await _service.GetPreviewDataAsync(new DataFilePreviewRequest
                    {
                        UserId = userId,
                        SourceDatabase = form.SourceDatabase,
                        SourceTable = form.SourceTable,
                        SourceSP = form.SourceSP,
                        ClientCode = request.ClientCode,
                        DateFrom = request.DateFrom,
                        DateTo = request.DateTo,
                        Parameters = request.Parameters,
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
                string zipFileName = $"{dataFileName}_({DateTimeHelper.GetCurrentLocalTime():yyyy-MM-dd_HHmm}).zip";
                string zipSubFileName = $"{dataFileName}_format_({DateTimeHelper.GetCurrentLocalTime():yyyy-MM-dd_HHmm})";

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