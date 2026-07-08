namespace DataWarehousePower.Models;

public sealed class HangfireJobDetailViewModel
{
    public int ScheduledJobId { get; set; }
    public string HangfireJobId { get; set; } = string.Empty;
    public bool ExistsInRecurringJobs { get; set; }
    public int SucceededCount { get; set; }
    public int FailedCount { get; set; }
    public DateTime? LastExecutedUtc { get; set; }
    public DateTime? LastSucceededUtc { get; set; }
    public DateTime? LastFailedUtc { get; set; }
    public DateTime? NextExecutionUtc { get; set; }
    public string? LastJobId { get; set; }
    public string? LastJobState { get; set; }
    public string? LastError { get; set; }
}
