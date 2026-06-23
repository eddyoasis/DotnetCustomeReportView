using DataWarehousePower.Authorization;
using DataWarehousePower.Models;
using DataWarehousePower.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DataWarehousePower.Controllers
{
    [Authorize(Policy = DepartmentAuthorizationPolicies.AuditlogAccess)]
    public class AuditLogController : Controller
    {
        private readonly IAuditLogQueryService _service;

        public AuditLogController(IAuditLogQueryService service)
        {
            _service = service;
        }

        public async Task<IActionResult> Index(
            string? userId,
            string? actionType,
            string? entityName,
            string? ipAddress,
            string? host,
            DateTime? dateFromUtc,
            DateTime? dateToUtc,
            string? search,
            int pageNumber = 1,
            int pageSize = 25)
        {
            AuditLogFilterViewModel filter = new()
            {
                UserId = userId,
                ActionType = actionType,
                EntityName = entityName,
                IpAddress = ipAddress,
                Host = host,
                DateFromUtc = dateFromUtc,
                DateToUtc = dateToUtc,
                Search = search,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            AuditLogListViewModel vm = await _service.GetListAsync(filter);
            return View(vm);
        }

        public async Task<IActionResult> Details(long id)
        {
            AuditLogDetailViewModel? vm = await _service.GetDetailAsync(id);
            if (vm is null)
            {
                return NotFound();
            }

            return View(vm);
        }
    }
}
