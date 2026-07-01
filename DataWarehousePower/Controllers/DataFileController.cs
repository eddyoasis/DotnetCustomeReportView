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

        public async Task<IActionResult> Index(int? id = null, string? search = null, bool? isActive = null)
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

            ViewData["DepartmentLookup"] = await GetDepartmentLookupAsync();

            return View(new DataFileBrowserViewModel
            {
                Filter = listViewModel.Filter,
                DataFiles = listViewModel.DataFiles,
                SelectedDataFile = selectedDataFile
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

        private async Task<Dictionary<int, string>> GetDepartmentLookupAsync()
        {
            return (await _departmentService.GetAllAsync())
                .GroupBy(department => department.Id)
                .ToDictionary(group => group.Key, group => group.First().Name);
        }

        private string? ResolveUserDepartment()
            => HttpContext.Session.GetString("UserDepartment");
    }
}