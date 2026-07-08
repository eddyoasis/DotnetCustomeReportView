using DataWarehousePower.Models;

namespace DataWarehousePower.Services;

public interface IHangfireJobDetailService
{
    Task<HangfireJobDetailViewModel?> GetJobDetailAsync(int scheduledJobId, string userId);
}
