using DataWarehousePower.Authorization;
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

        public DataFileController(
            IDataFileManageService service,
            IScheduledReportJobService scheduledReportJobService,
            IColumnPreferenceService prefService,
            IDepartmentService departmentService)
        {
            _service = service;
            _scheduledReportJobService = scheduledReportJobService;
            _prefService = prefService;
            _departmentService = departmentService;
        }

        public async Task<IActionResult> Index(int? id = null, string? search = null, bool? isActive = null, string? schemaTemplate = null)
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
            (List<ColumnDefinition> displayColumns, int? activePreferenceId) = await _prefService.LoadColumnPreferencesAsync(
                userId,
                selectedDataFile.Id,
                normalizedSchemaTemplate,
                availableColumns);

            List<string> savedSchemaTemplates = await _prefService.GetSchemaTemplatesAsync(userId, selectedDataFile.Id);
            Dictionary<string, int> schemaTemplatePreferenceIds = await _prefService.GetSchemaTemplatePreferenceIdsAsync(userId, selectedDataFile.Id);
            List<string> availableSchemaTemplates = BuildAvailableSchemaTemplates(normalizedSchemaTemplate, savedSchemaTemplates);

            ViewData["DepartmentLookup"] = await GetDepartmentLookupAsync();

            return View(new DataFileBrowserViewModel
            {
                Filter = listViewModel.Filter,
                DataFiles = listViewModel.DataFiles,
                SelectedDataFile = selectedDataFile,
                SchemaTemplate = normalizedSchemaTemplate,
                ActivePreferenceId = activePreferenceId,
                AvailableSchemaTemplates = availableSchemaTemplates,
                SchemaTemplatePreferenceIds = schemaTemplatePreferenceIds,
                AvailableColumns = availableColumns,
                DisplayColumns = displayColumns
            });
        }

        public async Task<IActionResult> List(string? search = null, bool? isActive = null)
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
                isActive
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
                int preferenceId = await _prefService.SavePreferencesAsync(userId, id, request.SchemaTemplate, request.PreferenceId, request.Columns, systemColumns);
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

        private string? ResolveUserDepartment()
            => HttpContext.Session.GetString("UserDepartment");
    }
}