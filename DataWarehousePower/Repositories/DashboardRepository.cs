using DataWarehousePower.Data;
using DataWarehousePower.Models;
using Microsoft.EntityFrameworkCore;

namespace DataWarehousePower.Repositories
{
    public class DashboardRepository : IDashboardRepository
    {
        private readonly AppDbContext _context;

        public DashboardRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<DashboardReportItem>> GetUserReportsAsync(string? userDepartment)
        {
            int? userDepartmentId = await ResolveUserDepartmentIdAsync(userDepartment);

            List<DashboardReportItem> reports = await _context.ReportDefinitions
                .AsNoTracking()
                .OrderBy(report => report.ReportName)
                .Select(report => new DashboardReportItem
                {
                    Id = report.Id,
                    Name = report.ReportName,
                    IsActive = report.IsActive,
                    Departments = report.Departments
                })
                .ToListAsync();

            return reports
                .Where(report => IsVisibleToDepartment(report.Departments, userDepartment, userDepartmentId))
                .ToList();
        }

        public async Task<List<DashboardDataFileItem>> GetUserDataFilesAsync(string userId, string? userDepartment)
        {
            int? userDepartmentId = await ResolveUserDepartmentIdAsync(userDepartment);

            List<DashboardDataFileItem> dataFiles = await _context.DataFileDefinitions
                .AsNoTracking()
                .OrderBy(dataFile => dataFile.DataFileName)
                .Select(dataFile => new DashboardDataFileItem
                {
                    Id = dataFile.Id,
                    Name = dataFile.DataFileName,
                    IsActive = dataFile.IsActive,
                    OwnerUserId = dataFile.UserId,
                    Departments = dataFile.Departments
                })
                .ToListAsync();

            return dataFiles
                .Where(dataFile =>
                    string.Equals(dataFile.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase) ||
                    IsVisibleToDepartment(dataFile.Departments, userDepartment, userDepartmentId))
                .ToList();
        }

        public async Task<List<DashboardScheduledJobItem>> GetUserScheduledJobsAsync(string userId)
        {
            List<DashboardScheduledJobItem> ScheduledJobs = await _context.ScheduledReportJobs
                .AsNoTracking()
                .OrderBy(ScheduledJob => ScheduledJob.JobName)
                .Select(ScheduledJob => new DashboardScheduledJobItem
                {
                    Id = ScheduledJob.Id,
                    Name = ScheduledJob.JobName,
                    IsActive = ScheduledJob.IsActive,
                    OwnerUserId = ScheduledJob.CreatedByUserId
                })
                .ToListAsync();

            return ScheduledJobs
                .Where(ScheduledJob => string.Equals(ScheduledJob.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        private async Task<int?> ResolveUserDepartmentIdAsync(string? userDepartment)
        {
            if (string.IsNullOrWhiteSpace(userDepartment))
            {
                return null;
            }

            string normalizedUserDepartment = userDepartment.Trim();

            return await _context.Departments
                .AsNoTracking()
                .Where(department => department.IsActive)
                .Where(department => department.Name == normalizedUserDepartment)
                .Select(department => (int?)department.Id)
                .FirstOrDefaultAsync();
        }

        private static bool IsVisibleToDepartment(string? itemDepartments, string? userDepartment, int? userDepartmentId)
        {
            if (string.IsNullOrWhiteSpace(itemDepartments))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(userDepartment))
            {
                return false;
            }

            string normalizedUserDepartment = userDepartment.Trim();

            HashSet<string> configuredDepartments = itemDepartments
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (configuredDepartments.Contains(normalizedUserDepartment))
            {
                return true;
            }

            if (!userDepartmentId.HasValue)
            {
                return false;
            }

            string userDepartmentIdToken = userDepartmentId.Value.ToString();
            return configuredDepartments.Contains(userDepartmentIdToken);
        }
    }
}
