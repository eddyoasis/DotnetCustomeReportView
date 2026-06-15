using System.ComponentModel.DataAnnotations;

namespace DataWarehousePower.Models;

public sealed class ScheduledJobListItemViewModel
{
    public int Id { get; set; }
    public string JobName { get; set; } = string.Empty;
    public string HangfireJobId { get; set; } = string.Empty;
    public int ReportDefinitionId { get; set; }
    public string ReportName { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string? ClientCode { get; set; }
    public string? FilterClientCode { get; set; }
    public string CronExpression { get; set; } = string.Empty;
    public string ScheduleDisplay { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public sealed class ScheduledJobFilterViewModel
{
    public string? JobName { get; set; }
    public string? ReportName { get; set; }
    public string? Format { get; set; }
    public string? ClientCode { get; set; }
    public string? FilterClientCode { get; set; }
    public bool? IsActive { get; set; }
}

public sealed class ScheduledJobListViewModel
{
    public List<ScheduledJobListItemViewModel> Jobs { get; set; } = [];
    public ScheduledJobFilterViewModel Filter { get; set; } = new();
    public List<string> AvailableFormats { get; set; } = [];
    public List<string> AvailableClientCodes { get; set; } = [];
    public List<string> AvailableFilterClientCodes { get; set; } = [];
}

public sealed class ScheduledJobFormViewModel
{
    public const string ScheduleTypeEveryMinutes = "every-minutes";
    public const string ScheduleTypeDailyTime = "daily-time";
    public const string ScheduleTypeAdvancedCron = "advanced-cron";

    public int Id { get; set; }

    [Required]
    [MaxLength(128)]
    [Display(Name = "Job Name")]
    public string JobName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Report")]
    public int ReportDefinitionId { get; set; }

    [Required]
    [RegularExpression("^(csv|excel|pdf)$", ErrorMessage = "Format must be csv, excel, or pdf.")]
    [Display(Name = "Format")]
    public string Format { get; set; } = "csv";

    [Required]
    [Display(Name = "Schedule Type")]
    public string ScheduleType { get; set; } = ScheduleTypeDailyTime;

    [Range(1, 1440)]
    [Display(Name = "Every (minutes)")]
    public int? EveryMinutes { get; set; } = 5;

    [MaxLength(5)]
    [Display(Name = "Daily Time")]
    public string DailyTime { get; set; } = "08:30";

    [MaxLength(128)]
    [Display(Name = "Cron Expression (Advanced)")]
    public string CronExpression { get; set; } = "0 8 * * *";

    [MaxLength(128)]
    [Display(Name = "Client Code")]
    public string? ClientCode { get; set; }

    [MaxLength(128)]
    [Display(Name = "Filter Client Code")]
    public string? FilterClientCode { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Date From")]
    public DateTime? DateFrom { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Date To")]
    public DateTime? DateTo { get; set; }

    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Update Password")]
    public bool UpdatePassword { get; set; }

    [Display(Name = "Existing Password")]
    public string ExistingPassword { get; set; } = string.Empty;

    [Display(Name = "Enabled")]
    public bool IsActive { get; set; } = true;

    public List<ReportDefinitionLookupItem> AvailableReports { get; set; } = [];
    public List<string> AvailableClientCodes { get; set; } = [];

    public Dictionary<int, List<string>> AvailableClientCodesByReportId { get; set; } = [];
}

public sealed class ReportDefinitionLookupItem
{
    public int Id { get; set; }
    public string ReportName { get; set; } = string.Empty;
}
