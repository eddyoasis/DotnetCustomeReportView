using DataWarehousePower.Authorization;
using DataWarehousePower.Models;
using DataWarehousePower.Services;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using DataWarehousePower.Helper;
using DataWarehousePower.Models.AppSettings;
using Microsoft.Extensions.Options;
using System.Linq;

namespace DataWarehousePower.Controllers
{
    /// <summary>
    /// CRUD management for DataFileDefinitions and their DataFileColumns.
    /// Route: /DataFileManage
    /// </summary>
    [Authorize(Policy = DepartmentAuthorizationPolicies.DataFileManageAccess)]
    public class DataFileManageController : Controller
    {
        private readonly IDataFileManageService _service;
        private readonly IScheduledReportJobService _scheduledReportJobService;
        private readonly IDepartmentService _departmentService;
        private readonly IDepartmentConnectionService _departmentConnectionService;
        private readonly IColumnPreferenceService _prefService;
        private readonly IAuditLogService _auditLogService;
        private readonly ILogger<DataFileManageController> _logger;
        private readonly GeneralAppSetting _generalAppSetting;
        private readonly ScheduledJob _scheduledJobAppSetting;

        public DataFileManageController(
            IOptionsSnapshot<ScheduledJob> scheduledJobAppSetting,
            IDataFileManageService service,
            IScheduledReportJobService scheduledReportJobService,
            IDepartmentService departmentService,
            IDepartmentConnectionService departmentConnectionService,
            IColumnPreferenceService prefService,
            IAuditLogService auditLogService,
            IOptionsSnapshot<GeneralAppSetting> generalAppSetting,
            ILogger<DataFileManageController> logger)
        {
            _scheduledJobAppSetting = scheduledJobAppSetting.Value;
            _service = service;
            _scheduledReportJobService = scheduledReportJobService;
            _departmentService = departmentService;
            _departmentConnectionService = departmentConnectionService;
            _prefService = prefService;
            _auditLogService = auditLogService;
            _logger = logger;
            _generalAppSetting = generalAppSetting.Value;
        }

        // GET /DataFileManage
        public async Task<IActionResult> Index(string? search, bool? isActive)
        {
            var filter = new DataFileManageFilterViewModel
            {
                Search = search,
                IsActive = isActive
            };

            var userId = HttpHelper.ResolveUserId(HttpContext);
            var userDepartment = HttpHelper.ResolveUserDepartment(HttpContext);

            //var vm = await _service.GetListViewModelAsync(userId, userDepartment, filter);
            var vm = await _service.GetListViewModelAsync(userId, filter);
            ViewData["DepartmentLookup"] = (await _departmentService.GetAllAsync())
                .GroupBy(department => department.Id)
                .ToDictionary(group => group.Key, group => group.First().Name);
            return View(vm);
        }

        // GET /DataFileManage/Create
        public async Task<IActionResult> Create()
        {
            var userDepartment = HttpHelper.ResolveUserDepartment(HttpContext);
            var isITDepartment = _generalAppSetting.InformationTechnologyDepartments.Contains(userDepartment);

            var vm = new DataFileManageFormViewModel
            {
                IsITDepartment = isITDepartment,
                Columns = new List<DataFileColumnFormModel>
                {
                    //new() { PropertyName = "", DefaultLabel = "", DisplayOrder = 1 }
                }
            };

            if (isITDepartment)
            {
                await PopulateSourceDatabaseOptionsAsync(vm);
                await PopulateSourceTableOptionsAsync(vm);
                await PopulateSourceSPOptionsAsync(vm);

            }
            else
            {
                var departmentConnection = await _departmentConnectionService.GetConnectionStringByUserDepartmentAsync(userDepartment);

                await PopulateSourceDatabaseAsync(vm, departmentConnection);
                await PopulateSourceTableOptionsAsync(vm, departmentConnection);
                await PopulateSourceSPOptionsAsync(vm, departmentConnection);
            }
            
            await PopulateDepartmentOptionsAsync(vm);
            return View("Form", vm);
        }

        // GET /DataFileManage/Edit/{id}
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var userDepartment = HttpHelper.ResolveUserDepartment(HttpContext);
                var isITDepartment = _generalAppSetting.InformationTechnologyDepartments.Contains(userDepartment);

                var vm = await _service.GetFormViewModelAsync(id);
                vm.IsITDepartment = isITDepartment;

                if (isITDepartment)
                {
                    await PopulateSourceDatabaseOptionsAsync(vm);
                    await PopulateSourceTableOptionsAsync(vm);
                    await PopulateSourceSPOptionsAsync(vm);

                }
                else
                {
                    var departmentConnection = await _departmentConnectionService.GetConnectionStringByUserDepartmentAsync(userDepartment);

                    await PopulateSourceDatabaseAsync(vm, departmentConnection);
                    await PopulateSourceTableOptionsAsync(vm, departmentConnection);
                    await PopulateSourceSPOptionsAsync(vm, departmentConnection);
                }

                await PopulateDepartmentOptionsAsync(vm);
                return View("Form", vm);
            }
            catch (InvalidOperationException)
            {
                return NotFound();
            }
        }

        [HttpGet]
        public async Task<IActionResult> ScheduleDataFile(int id, string? returnUrl = null)
        {
            string userId = _prefService.ResolveUserId(HttpContext);
            int? existingJobId = await _scheduledReportJobService.FindExistingJobIdAsync(userId, id, null, null);

            if (existingJobId.HasValue)
            {
                return RedirectToAction("Edit", "ScheduledJob", new { id = existingJobId.Value });
            }

            return RedirectToAction("Create", "ScheduledJob", new
            {
                reportDefinitionId = id,
                schemaTemplate = (string?)null,
                clientCode = (string?)null,
                returnUrl,
                isDataFile = true
            });
        }

        // POST /DataFileManage/Save (handles both Create and Edit)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(DataFileManageFormViewModel form)
        {
            string userId = _prefService.ResolveUserId(HttpContext);
            string username = ResolveAuditUsername();
            string correlationId = HttpContext.TraceIdentifier;

            for (int i = 0; i < form.Columns.Count; i++)
            {
                if (form.Columns[i].IsDeleted)
                {
                    ModelState.Remove($"Columns[{i}].PropertyName");
                    ModelState.Remove($"Columns[{i}].DefaultLabel");
                }
            }

            await NormalizeDepartmentSelectionsAsync(form);

            if (!ModelState.IsValid)
            {
                await PopulateSourceDatabaseOptionsAsync(form);
                await PopulateSourceTableOptionsAsync(form);
                await PopulateSourceSPOptionsAsync(form);
                await PopulateDepartmentOptionsAsync(form);
                return View("Form", form);
            }

            if (!form.Columns.Any(column => !column.IsDeleted))
            {
                ModelState.AddModelError("", "At least one column is required.");
                await PopulateSourceDatabaseOptionsAsync(form);
                await PopulateSourceTableOptionsAsync(form);
                await PopulateSourceSPOptionsAsync(form);
                await PopulateDepartmentOptionsAsync(form);
                return View("Form", form);
            }

            bool hasTable = !string.IsNullOrWhiteSpace(form.SourceTable);
            bool hasSP = !string.IsNullOrWhiteSpace(form.SourceSP);

            if (!hasTable && !hasSP)
            {
                ModelState.AddModelError("", "Provide either a Source Table or a Source Stored Procedure.");
                await PopulateSourceDatabaseOptionsAsync(form);
                await PopulateSourceTableOptionsAsync(form);
                await PopulateSourceSPOptionsAsync(form);
                await PopulateDepartmentOptionsAsync(form);
                return View("Form", form);
            }
            if (hasTable && hasSP)
            {
                ModelState.AddModelError("", "Provide either a Source Table or a Source Stored Procedure - not both.");
                await PopulateSourceDatabaseOptionsAsync(form);
                await PopulateSourceTableOptionsAsync(form);
                await PopulateSourceSPOptionsAsync(form);
                await PopulateDepartmentOptionsAsync(form);
                return View("Form", form);
            }

            string? requiredDateFilterColumnValidationError = ValidateRequiredDateFilterColumn(form);
            if (!string.IsNullOrWhiteSpace(requiredDateFilterColumnValidationError))
            {
                ModelState.AddModelError("", requiredDateFilterColumnValidationError);
                await PopulateSourceDatabaseOptionsAsync(form);
                await PopulateSourceTableOptionsAsync(form);
                await PopulateSourceSPOptionsAsync(form);
                await PopulateDepartmentOptionsAsync(form);
                return View("Form", form);
            }

            //string? requiredMappingValidationError = ValidateRequiredMappingParameters(form, hasTable, hasSP);
            //if (!string.IsNullOrWhiteSpace(requiredMappingValidationError))
            //{
            //    ModelState.AddModelError("", requiredMappingValidationError);
            //    await PopulateSourceDatabaseOptionsAsync(form);
            //    await PopulateSourceTableOptionsAsync(form);
            //    await PopulateSourceSPOptionsAsync(form);
            //    await PopulateDepartmentOptionsAsync(form);
            //    return View("Form", form);
            //}

            try
            {
                form.UserId = userId;

                if (form.Id == 0)
                {
                    DataFileDefinition created = await _service.CreateDataFileAsync(form);
                    await _auditLogService.LogActionAsync(
                        actionType: "DataFileManageCreateSucceeded",
                        userId: userId,
                        username: username,
                        correlationId: correlationId,
                        entityName: "DataFileDefinition",
                        entityId: created.Id.ToString(),
                        entityLabel: created.DataFileName,
                        oldValues: null,
                        newValues: new
                        {
                            created.Id,
                            created.DataFileName,
                            created.SourceDatabase,
                            created.SourceTable,
                            created.SourceSP,
                            created.Parameters,
                            created.IsActive,
                            created.Departments,
                            created.UserId,
                            created.CreatedBy,
                            created.CreatedAt,
                        },
                        detail: "Data file definition created.");
                }
                else
                {
                    DataFileManageFormViewModel? existing = null;
                    try
                    {
                        existing = await _service.GetFormViewModelAsync(form.Id);
                    }
                    catch (InvalidOperationException)
                    {
                        existing = null;
                    }

                    await _service.UpdateDataFileAsync(form);
                    await _auditLogService.LogActionAsync(
                        actionType: "DataFileManageUpdateSucceeded",
                        userId: userId,
                        username: username,
                        correlationId: correlationId,
                        entityName: "DataFileDefinition",
                        entityId: form.Id.ToString(),
                        entityLabel: form.DataFileName,
                        oldValues: existing is null ? null : new
                        {
                            existing.Id,
                            existing.DataFileName,
                            existing.SourceDatabase,
                            existing.SourceTable,
                            existing.SourceSP,
                            existing.Parameters,
                            existing.IsActive,
                            existing.Departments
                        },
                        newValues: new
                        {
                            form.Id,
                            form.DataFileName,
                            form.SourceDatabase,
                            form.SourceTable,
                            form.SourceSP,
                            form.Parameters,
                            form.IsActive,
                            form.Departments
                        },
                        detail: "Data file definition updated.");
                }

                TempData["Success"] = form.Id == 0
                    ? $"Data file \"{form.DataFileName}\" created successfully."
                    : $"Data file \"{form.DataFileName}\" updated successfully.";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save data file definition");
                await _auditLogService.LogActionAsync(
                    actionType: form.Id == 0 ? "DataFileManageCreateFailed" : "DataFileManageUpdateFailed",
                    userId: userId,
                    username: username,
                    correlationId: correlationId,
                    entityName: "DataFileDefinition",
                    entityId: form.Id == 0 ? null : form.Id.ToString(),
                    entityLabel: form.DataFileName,
                    oldValues: null,
                    newValues: new
                    {
                        form.Id,
                        form.DataFileName,
                        form.SourceDatabase,
                        form.SourceTable,
                        form.SourceSP,
                        form.Parameters,
                        form.IsActive,
                        form.Departments
                    },
                    detail: ex.Message);
                ModelState.AddModelError("", "An error occurred while saving. Please try again.");
                await PopulateSourceDatabaseOptionsAsync(form);
                await PopulateSourceTableOptionsAsync(form);
                await PopulateSourceSPOptionsAsync(form);
                await PopulateDepartmentOptionsAsync(form);
                return View("Form", form);
            }
        }

        [HttpGet]
        public async Task<IActionResult> SourceObjects(string? sourceDatabase)
        {
            try
            {
                var sourceTableOptions = await _service.GetSourceTableOptionsAsync(sourceDatabase);
                var items = sourceTableOptions.Where(x => x.Contains("VW_")).ToList();

                return Json(items);
            }
            catch (SqlException ex) when (ex.Number is 916 or 229)
            {
                _logger.LogWarning(ex, "Metadata access denied for source database {SourceDatabase}", sourceDatabase);
                return Json(Array.Empty<string>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> SourceProcedures(string? sourceDatabase)
        {
            try
            {
                var items = await _service.GetSourceStoredProcedureOptionsAsync(sourceDatabase);
                return Json(items);
            }
            catch (SqlException ex) when (ex.Number is 916 or 229)
            {
                _logger.LogWarning(ex, "Stored procedure metadata access denied for source database {SourceDatabase}", sourceDatabase);
                return Json(Array.Empty<string>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> SourceColumns(string? sourceDatabase, string? sourceTable, string? sourceSP)
        {
            try
            {
                var items = await _service.GetSourceColumnsAsync(sourceDatabase, sourceTable, sourceSP);
                return Json(items);
            }
            catch (SqlException ex) when (ex.Number is 916 or 229 or 11514)
            {
                _logger.LogWarning(ex,
                    "Column metadata access failed for source database {SourceDatabase}, source table {SourceTable}, source SP {SourceSP}",
                    sourceDatabase, sourceTable, sourceSP);
                return Json(Array.Empty<string>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> SourceColumnMetadata(string? sourceDatabase, string? sourceTable, string? sourceSP)
        {
            try
            {
                var items = await _service.GetSourceColumnMetadataAsync(sourceDatabase, sourceTable, sourceSP);
                return Json(items);
            }
            catch (SqlException ex) when (ex.Number is 916 or 229 or 11514)
            {
                _logger.LogWarning(ex,
                    "Column metadata(type) access failed for source database {SourceDatabase}, source table {SourceTable}, source SP {SourceSP}",
                    sourceDatabase, sourceTable, sourceSP);
                return Json(Array.Empty<SourceColumnMetadata>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> SourceParameters(string? sourceDatabase, string? sourceTable, string? sourceSP)
        {
            try
            {
                var items = await _service.GetSourceParametersAsync(sourceDatabase, sourceTable, sourceSP);
                return Json(items);
            }
            catch (SqlException ex) when (ex.Number is 916 or 229 or 11514)
            {
                _logger.LogWarning(ex,
                    "Parameter metadata access failed for source database {SourceDatabase}, source table {SourceTable}, source SP {SourceSP}",
                    sourceDatabase, sourceTable, sourceSP);
                return Json(Array.Empty<string>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> MappingParameterValues(
            string? sourceDatabase,
            string? sourceTable,
            string? sourceSP,
            string? columnName,
            string? search = null,
            int take = 50)
        {
            try
            {
                var items = await _service.GetDistinctColumnValuesAsync(
                    sourceDatabase,
                    sourceTable,
                    sourceSP,
                    columnName,
                    search,
                    take);

                return Json(items);
            }
            catch (SqlException ex) when (ex.Number is 916 or 229 or 911 or 11514)
            {
                _logger.LogWarning(ex,
                    "Mapping parameter values query failed for source database {SourceDatabase}, source table {SourceTable}, source SP {SourceSP}, column {ColumnName}",
                    sourceDatabase,
                    sourceTable,
                    sourceSP,
                    columnName);

                return Json(Array.Empty<string>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Preview([FromBody] DataFilePreviewRequest request)
        {
            try
            {
                if(request.DateTo.HasValue)
                    request.DateTo = request.DateTo.Value.AddDays(1).AddSeconds(-1);

                var userId = HttpHelper.ResolveUserId(HttpContext);
                request.UserId = userId;

                DataFilePreviewResult preview = await _service.GetPreviewDataAsync(request);
                return Json(new
                {
                    columns = preview.Columns,
                    rows = preview.Rows,
                    count = preview.TotalRowCount,
                    canExport = preview.TotalRowCount > 0 && preview.TotalRowCount <= _scheduledJobAppSetting.ExportSplit.MaxExportRecord
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (SqlException ex) when (ex.Number is 916 or 229 or 911 or 11514)
            {
                _logger.LogWarning(ex,
                    "Preview query failed for source database {SourceDatabase}, source table {SourceTable}, source SP {SourceSP}",
                    request?.SourceDatabase,
                    request?.SourceTable,
                    request?.SourceSP);
                return BadRequest(new { message = "Failed to load preview data due to source access restrictions." });
            }
        }

        private async Task PopulateSourceDatabaseOptionsAsync(DataFileManageFormViewModel vm)
        {
            vm.SourceDatabaseOptions = await _service.GetSourceDatabaseOptionsAsync();
        }

        private async Task PopulateSourceDatabaseAsync(DataFileManageFormViewModel vm, string dbConnectionString)
        {
            vm.SourceDatabaseOptions = await _service.GetSourceDatabaseAsync(dbConnectionString);
            vm.SourceDatabase = vm.SourceDatabaseOptions.FirstOrDefault();
        }

        private async Task PopulateSourceDatabaseOptionsAsync(DataFileManageFormViewModel vm, string dbConnectionString)
        {
            vm.SourceDatabaseOptions = await _service.GetSourceDatabaseOptionsAsync(dbConnectionString);
        }

        private async Task PopulateSourceTableOptionsAsync(DataFileManageFormViewModel vm)
        {
            var sourceTableOptions = await _service.GetSourceTableOptionsAsync(vm.SourceDatabase);
            vm.SourceTableOptions = sourceTableOptions.Where(x => x.Contains("VW_")).ToList();
        }

        private async Task PopulateSourceTableOptionsAsync(DataFileManageFormViewModel vm, string dbConnectionString)
        {
            var sourceTableOptions = await _service.GetSourceTableOptionsAsync(dbConnectionString, vm.SourceDatabase);
            vm.SourceTableOptions = sourceTableOptions.Where(x => x.Contains("VW_")).ToList();
        }

        private async Task PopulateSourceSPOptionsAsync(DataFileManageFormViewModel vm)
        {
            vm.SourceSPOptions = await _service.GetSourceStoredProcedureOptionsAsync(vm.SourceDatabase);
        }

        private async Task PopulateSourceSPOptionsAsync(DataFileManageFormViewModel vm, string dbConnectionString)
        {
            vm.SourceSPOptions = await _service.GetSourceStoredProcedureOptionsAsync(dbConnectionString, vm.SourceDatabase);
        }

        private async Task PopulateDepartmentOptionsAsync(DataFileManageFormViewModel vm)
        {
            List<Department> activeDepartments = (await _departmentService.GetAllAsync())
                .Where(department => department.IsActive)
                .OrderBy(department => department.Name)
                .ToList();

            vm.ActiveDepartmentOptions = activeDepartments
                .Select(department => new DepartmentSelectionItem
                {
                    Id = department.Id,
                    Name = department.Name
                })
                .ToList();

            if (vm.SelectedDepartmentIds.Count > 0)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(vm.Departments))
            {
                return;
            }

            var selectedIds = new HashSet<int>();
            var departmentsByName = activeDepartments
                .GroupBy(department => department.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.OrdinalIgnoreCase);

            foreach (string token in vm.Departments.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(token, out int departmentId))
                {
                    if (activeDepartments.Any(department => department.Id == departmentId))
                    {
                        selectedIds.Add(departmentId);
                    }
                    continue;
                }

                if (departmentsByName.TryGetValue(token, out int mappedId))
                {
                    selectedIds.Add(mappedId);
                }
            }

            vm.SelectedDepartmentIds = selectedIds.OrderBy(id => id).ToList();
        }

        private async Task NormalizeDepartmentSelectionsAsync(DataFileManageFormViewModel vm)
        {
            HashSet<int> activeDepartmentIds = (await _departmentService.GetAllAsync())
                .Where(department => department.IsActive)
                .Select(department => department.Id)
                .ToHashSet();

            vm.SelectedDepartmentIds = vm.SelectedDepartmentIds
                .Where(id => activeDepartmentIds.Contains(id))
                .Distinct()
                .OrderBy(id => id)
                .ToList();

            vm.Departments = vm.SelectedDepartmentIds.Count == 0
                ? null
                : string.Join(',', vm.SelectedDepartmentIds);
        }

        private static string? ValidateRequiredMappingParameters(DataFileManageFormViewModel form, bool hasTable, bool hasSP)
        {
            HashSet<string> normalizedMappings = new(StringComparer.OrdinalIgnoreCase);

            foreach (DataFileColumnFormModel column in form.Columns.Where(column => !column.IsDeleted))
            {
                foreach (string mapping in SplitAndNormalizeMappings(column.MappingParameter))
                {
                    normalizedMappings.Add(mapping);
                }
            }

            if (hasSP)
            {
                foreach (string mapping in ParseConfiguredParameterMappings(form.Parameters))
                {
                    normalizedMappings.Add(mapping);
                }
            }

            bool hasClientCode = normalizedMappings.Contains("ClientCode");
            bool hasDateFrom = normalizedMappings.Contains("FilterDateFrom");
            bool hasDateTo = normalizedMappings.Contains("FilterDateTo");

            if (!hasClientCode || !hasDateFrom || !hasDateTo)
            {
                return "Please map ClientCode, FilterDateFrom, and FilterDateTo before saving the data file.";
            }

            return null;
        }

        private static string? ValidateRequiredDateFilterColumn(DataFileManageFormViewModel form)
        {
            //bool hasDateFilterColumn = form.Columns
            //    .Where(column => !column.IsDeleted)
            //    .Any(column => string.Equals(
            //        column.MappingParameterFilter,
            //        "FilterDateFrom,FilterDateTo",
            //        StringComparison.OrdinalIgnoreCase));

            //if (!hasDateFilterColumn)
            //{
            //    return "Please select a date column for filter datefrom/dateto";
            //}

            return null;
        }

        private static IEnumerable<string> SplitAndNormalizeMappings(string? rawMappings)
        {
            return (rawMappings ?? string.Empty)
                .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(NormalizeMappingAlias)
                .Where(value => !string.IsNullOrWhiteSpace(value));
        }

        private static IEnumerable<string> ParseConfiguredParameterMappings(string? rawParametersJson)
        {
            if (string.IsNullOrWhiteSpace(rawParametersJson))
            {
                return Enumerable.Empty<string>();
            }

            try
            {
                List<ReportParameterFormModel>? parsed = JsonSerializer.Deserialize<List<ReportParameterFormModel>>(rawParametersJson);
                if (parsed is null || parsed.Count == 0)
                {
                    return Enumerable.Empty<string>();
                }

                return parsed
                    .Select(parameter => NormalizeMappingAlias(parameter.MappingParameter))
                    .Where(value => !string.IsNullOrWhiteSpace(value));
            }
            catch
            {
                return Enumerable.Empty<string>();
            }
        }

        private static string NormalizeMappingAlias(string? mapping)
        {
            string normalized = (mapping ?? string.Empty).Trim().TrimStart('@');

            if (normalized.Equals("DateFrom", StringComparison.OrdinalIgnoreCase))
            {
                return "FilterDateFrom";
            }

            if (normalized.Equals("DateTo", StringComparison.OrdinalIgnoreCase))
            {
                return "FilterDateTo";
            }

            if (normalized.Equals("FilterClientCode", StringComparison.OrdinalIgnoreCase))
            {
                return "ClientCode";
            }

            return normalized;
        }

        // POST /DataFileManage/Delete/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            string userId = _prefService.ResolveUserId(HttpContext);
            string username = ResolveAuditUsername();
            string correlationId = HttpContext.TraceIdentifier;
            DataFileManageFormViewModel? existing = null;

            try
            {
                existing = await _service.GetFormViewModelAsync(id);
            }
            catch (InvalidOperationException)
            {
                existing = null;
            }

            try
            {
                await _service.DeleteAsync(id);
                await _auditLogService.LogActionAsync(
                    actionType: "DataFileManageDeleteSucceeded",
                    userId: userId,
                    username: username,
                    correlationId: correlationId,
                    entityName: "DataFileDefinition",
                    entityId: id.ToString(),
                    entityLabel: existing?.DataFileName,
                    oldValues: existing is null ? null : new
                    {
                        existing.Id,
                        existing.DataFileName,
                        existing.SourceDatabase,
                        existing.SourceTable,
                        existing.SourceSP,
                        existing.Parameters,
                        existing.IsActive,
                        existing.Departments
                    },
                    newValues: null,
                    detail: "Data file definition deleted.");
                TempData["Success"] = "Data file definition deleted successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete data file definition {DataFileDefinitionId}", id);
                await _auditLogService.LogActionAsync(
                    actionType: "DataFileManageDeleteFailed",
                    userId: userId,
                    username: username,
                    correlationId: correlationId,
                    entityName: "DataFileDefinition",
                    entityId: id.ToString(),
                    entityLabel: existing?.DataFileName,
                    oldValues: existing is null ? null : new
                    {
                        existing.Id,
                        existing.DataFileName,
                        existing.SourceDatabase,
                        existing.SourceTable,
                        existing.SourceSP,
                        existing.Parameters,
                        existing.IsActive,
                        existing.Departments
                    },
                    newValues: null,
                    detail: ex.Message);
                TempData["Error"] = "Failed to delete the data file definition.";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST /DataFileManage/ToggleActive/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            string userId = _prefService.ResolveUserId(HttpContext);
            string username = ResolveAuditUsername();
            string correlationId = HttpContext.TraceIdentifier;
            DataFileManageFormViewModel? existing = null;

            try
            {
                existing = await _service.GetFormViewModelAsync(id);
            }
            catch (InvalidOperationException)
            {
                existing = null;
            }

            try
            {
                await _service.ToggleActiveAsync(id);

                bool? newIsActive = existing is null ? null : !existing.IsActive;
                await _auditLogService.LogActionAsync(
                    actionType: "DataFileManageToggleActiveSucceeded",
                    userId: userId,
                    username: username,
                    correlationId: correlationId,
                    entityName: "DataFileDefinition",
                    entityId: id.ToString(),
                    entityLabel: existing?.DataFileName,
                    oldValues: existing is null ? null : new { existing.Id, existing.IsActive },
                    newValues: newIsActive is null ? null : new { Id = id, IsActive = newIsActive.Value },
                    detail: "Data file definition active status toggled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to toggle active status for data file definition {DataFileDefinitionId}", id);
                await _auditLogService.LogActionAsync(
                    actionType: "DataFileManageToggleActiveFailed",
                    userId: userId,
                    username: username,
                    correlationId: correlationId,
                    entityName: "DataFileDefinition",
                    entityId: id.ToString(),
                    entityLabel: existing?.DataFileName,
                    oldValues: existing is null ? null : new { existing.Id, existing.IsActive },
                    newValues: null,
                    detail: ex.Message);
                TempData["Error"] = "Failed to update data file definition status.";
            }

            return RedirectToAction(nameof(Index));
        }

        private string ResolveAuditUsername()
        {
            string? displayName = HttpContext.Session.GetString("UserDisplayName");
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                return displayName.Trim();
            }

            return string.IsNullOrWhiteSpace(User.Identity?.Name) ? "Anonymous" : User.Identity!.Name!.Trim();
        }
    }
}
