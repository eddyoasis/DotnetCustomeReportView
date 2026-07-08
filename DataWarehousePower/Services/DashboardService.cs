using DataWarehousePower.Models;
using DataWarehousePower.Repositories;

namespace DataWarehousePower.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly IDashboardRepository _dashboardRepository;
        private readonly IHangfireJobDetailService _hangfireJobDetailService;

        public DashboardService(
            IDashboardRepository dashboardRepository, 
            IHangfireJobDetailService hangfireJobDetailService)
        {
            _dashboardRepository = dashboardRepository;
            _hangfireJobDetailService = hangfireJobDetailService;
        }

        public async Task<DashboardViewModel> BuildDashboardViewModelAsync(string userId, string? userDepartment)
        {
            List<DashboardReportItem> reports = await _dashboardRepository.GetUserReportsAsync(userDepartment);
            List<DashboardDataFileItem> dataFiles = await _dashboardRepository.GetUserDataFilesAsync(userId);
            List<DashboardScheduledJobItem> scheduledJobs = await _dashboardRepository.GetUserScheduledJobsAsync(userId);

            foreach (var item in scheduledJobs)
            {
                var jobDetail = await _hangfireJobDetailService.GetJobDetailAsync(item.Id, userId);
                item.LastSucceeded = jobDetail.LastSucceededUtc.HasValue ? jobDetail.LastSucceededUtc.Value.AddHours(8) : null;
                item.NextExecution = jobDetail.NextExecutionUtc.HasValue ? jobDetail.NextExecutionUtc.Value : null;
            }

            return new DashboardViewModel
            {
                UserId = userId,
                UserDepartment = string.IsNullOrWhiteSpace(userDepartment) ? null : userDepartment.Trim(),
                TotalReports = reports.Count,
                ActiveReports = reports.Count(report => report.IsActive),
                TotalDataFiles = dataFiles.Count,
                ActiveDataFiles = dataFiles.Count(dataFile => dataFile.IsActive),
                TotalScheduledJobs = scheduledJobs.Count,
                ActiveScheduledJobs = scheduledJobs.Count(dataFile => dataFile.IsActive),
                Reports = reports,
                DataFiles = dataFiles,
                ScheduledJobs = scheduledJobs
            };
        }

        //public async Task<DashboardViewModel> BuildDashboardViewModelAsync(string userId, string? userDepartment)
        //{
        //    Task<List<DashboardReportItem>> reportsTask = _dashboardRepository.GetUserReportsAsync(userDepartment);
        //    Task<List<DashboardDataFileItem>> dataFilesTask = _dashboardRepository.GetUserDataFilesAsync(userId, userDepartment);

        //    await Task.WhenAll(reportsTask, dataFilesTask);

        //    List<DashboardReportItem> reports = reportsTask.Result;
        //    List<DashboardDataFileItem> dataFiles = dataFilesTask.Result;

        //    return new DashboardViewModel
        //    {
        //        UserId = userId,
        //        UserDepartment = string.IsNullOrWhiteSpace(userDepartment) ? null : userDepartment.Trim(),
        //        TotalReports = reports.Count,
        //        ActiveReports = reports.Count(report => report.IsActive),
        //        TotalDataFiles = dataFiles.Count,
        //        ActiveDataFiles = dataFiles.Count(dataFile => dataFile.IsActive),
        //        Reports = reports,
        //        DataFiles = dataFiles
        //    };
        //}
    }
}
