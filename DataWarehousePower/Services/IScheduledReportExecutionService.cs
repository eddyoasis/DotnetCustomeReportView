namespace DataWarehousePower.Services;

public interface IScheduledReportExecutionService
{
    Task ExecuteAsync(int scheduledJobId);
}
