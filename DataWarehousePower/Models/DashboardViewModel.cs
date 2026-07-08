namespace DataWarehousePower.Models
{
    public sealed class DashboardViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string? UserDepartment { get; set; }
        public int TotalReports { get; set; }
        public int ActiveReports { get; set; }
        public int TotalDataFiles { get; set; }
        public int ActiveDataFiles { get; set; }
        public int TotalScheduledJobs { get; set; }
        public int ActiveScheduledJobs { get; set; }
        public List<DashboardReportItem> Reports { get; set; } = new();
        public List<DashboardDataFileItem> DataFiles { get; set; } = new();
        public List<DashboardScheduledJobItem> ScheduledJobs { get; set; } = new();
    }

    public sealed class DashboardReportItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string? Departments { get; set; }
    }

    public sealed class DashboardDataFileItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string? OwnerUserId { get; set; }
        public string? Departments { get; set; }
    }

    public sealed class DashboardScheduledJobItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string? OwnerUserId { get; set; }

        public DateTime? LastSucceeded { get; set; }

    }
}
