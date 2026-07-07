using DataWarehousePower.Authorization;
using DataWarehousePower.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DataWarehousePower.Controllers
{
    [Authorize(Policy = DepartmentAuthorizationPolicies.ReportAccess)]
    public class DashboardController : Controller
    {
        private readonly IDashboardService _dashboardService;
        private readonly IColumnPreferenceService _columnPreferenceService;

        public DashboardController(
            IDashboardService dashboardService,
            IColumnPreferenceService columnPreferenceService)
        {
            _dashboardService = dashboardService;
            _columnPreferenceService = columnPreferenceService;
        }

        public async Task<IActionResult> Index()
        {
            string userId = _columnPreferenceService.ResolveUserId(HttpContext);
            string? userDepartment = HttpContext.Session.GetString("UserDepartment");

            var viewModel = await _dashboardService.BuildDashboardViewModelAsync(userId, userDepartment);
            return View(viewModel);
        }
    }
}
