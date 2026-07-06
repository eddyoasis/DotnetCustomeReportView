using System.ComponentModel.DataAnnotations;

namespace DataWarehousePower.Models;

public sealed class ScheduledJobListItemViewModel
{
    public int Id { get; set; }
    public string JobName { get; set; } = string.Empty;
    public string HangfireJobId { get; set; } = string.Empty;
    public string SourceType { get; set; } = "Report";
    public int ReportDefinitionId { get; set; }
    public string ReportName { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string? SchemaTemplate { get; set; }
    public string? ClientCode { get; set; }
    public string CronExpression { get; set; } = string.Empty;
    public string ScheduleDisplay { get; set; } = string.Empty;
    public string JobAction { get; set; } = ScheduledJobActions.ExportFile;
    public string? RecipientEmail { get; set; }
    public string? ExportLocation { get; set; }
    public bool ExportToLocalFolder { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public sealed class ScheduledJobFilterViewModel
{
    public string? SourceType { get; set; }
    public string? JobName { get; set; }
    public string? ReportName { get; set; }
    public string? Format { get; set; }
    public string? JobAction { get; set; }
    public string? SchemaTemplate { get; set; }
    public string? ClientCode { get; set; }
    public bool? IsActive { get; set; }
}

public sealed class ScheduledJobListViewModel
{
    public List<ScheduledJobListItemViewModel> Jobs { get; set; } = [];
    public ScheduledJobFilterViewModel Filter { get; set; } = new();
    public List<string> AvailableFormats { get; set; } = [];
    public List<string> AvailableJobActions { get; set; } = [];
    public List<string> AvailableSchemaTemplates { get; set; } = [];
    public List<string> AvailableClientCodes { get; set; } = [];
    public List<ExportLocationBasePathOptionViewModel> AvailableExportLocationBasePathOptions { get; set; } = [];
}

public sealed class ScheduledJobFormViewModel
{
    public const int JobNameMaxLength = 128;
    public const int RecipientEmailMaxLength = 2048;
    public const string ScheduleTypeEveryMinutes = "every-minutes";
    public const string ScheduleTypeDailyTime = "daily-time";
    public const string ScheduleTypeCustomDays = "custom-days";
    public const string ScheduleTypeAdvancedCron = "advanced-cron";
    public const string AutoDateIntervalMinutely = "minutely";
    public const string AutoDateIntervalHourly = "hourly";
    public const string AutoDateIntervalDaily = "daily";
    public const string AutoDateIntervalWeekly = "weekly";
    public const string AutoDateIntervalMonthly = "monthly";
    public const string AutoDateIntervalYearly = "yearly";

    public int Id { get; set; }

    [MaxLength(JobNameMaxLength)]
    [Display(Name = "Job Name")]
    public string JobName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Report")]
    public int ReportDefinitionId { get; set; }

    [Display(Name = "Formats")]
    public List<string> Formats { get; set; } = ["csv"];

    [Required]
    [Display(Name = "Job Action")]
    public string JobAction { get; set; } = ScheduledJobActions.ExportFile;

    [MaxLength(RecipientEmailMaxLength)]
    [Display(Name = "Recipient Email")]
    public string? RecipientEmail { get; set; }

    [Required]
    [Display(Name = "Schedule Type")]
    public string ScheduleType { get; set; } = ScheduleTypeDailyTime;

    [Range(1, 1440)]
    [Display(Name = "Every (minutes)")]
    public int? EveryMinutes { get; set; } = 5;

    [MaxLength(5)]
    [Display(Name = "Daily Time")]
    public string DailyTime { get; set; } = "08:30";

    [MaxLength(5)]
    [Display(Name = "Execution Time")]
    public string CustomDaysTime { get; set; } = "08:30";

    [Display(Name = "Execution Days")]
    public List<int> SelectedWeekdays { get; set; } = [];

    [MaxLength(128)]
    [Display(Name = "Cron Expression (Advanced)")]
    public string CronExpression { get; set; } = "0 8 * * *";

    [MaxLength(128)]
    [Display(Name = "Schema Template")]
    public string? SchemaTemplate { get; set; }

    [MaxLength(128)]
    [Display(Name = "Client Code")]
    public string? ClientCode { get; set; }

    [Display(Name = "Requires Schema Template and Client Code")]
    public bool RequiresSchemaTemplateAndClientCode { get; set; } = true;

    [Display(Name = "Parameters")]
    public string? Parameters { get; set; }

    [MaxLength(512)]
    [Display(Name = "Export Location")]
    public string? ExportLocation { get; set; }

    [MaxLength(256)]
    [Display(Name = "Export Subfolder")]
    public string? ExportLocationSubfolder { get; set; }

    [Display(Name = "Export to User Remote Folder")]
    public bool ExportToLocalFolder { get; set; }

    public List<ExportLocationBasePathOptionViewModel> AvailableExportLocationBasePathOptions { get; set; } = [];

    public List<string> AvailableExportLocationBasePaths { get; set; } = [];

    public List<string> AvailableClientCodeFolders { get; set; } = [];

    [DataType(DataType.Date)]
    [Display(Name = "Date From")]
    public DateTime? DateFrom { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Date To")]
    public DateTime? DateTo { get; set; }

    [Display(Name = "Use Custom Date Range")]
    public bool IsCustom { get; set; } = true;

    [MaxLength(16)]
    [Display(Name = "Auto Date Interval")]
    public string AutoDateIntervalUnit { get; set; } = AutoDateIntervalDaily;

    [Range(1, int.MaxValue)]
    [Display(Name = "Interval Value")]
    public int? AutoDateIntervalValue { get; set; } = 1;

    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Update Password")]
    public bool UpdatePassword { get; set; }

    [Display(Name = "Existing Password")]
    public string ExistingPassword { get; set; } = string.Empty;

    [Display(Name = "Enabled")]
    public bool IsActive { get; set; } = true;

    public string? ReturnUrl { get; set; }

    public List<ReportDefinitionLookupItem> AvailableReports { get; set; } = [];
    public List<ReportDefinitionLookupItem> AvailableDataFiles { get; set; } = [];
    public List<string> AvailableSchemaTemplates { get; set; } = [];
    public List<string> AvailableClientCodes { get; set; } = [];

    public Dictionary<int, List<string>> AvailableSchemaTemplatesByReportId { get; set; } = [];

    public Dictionary<int, List<ScheduledJobParameterInputViewModel>> AvailableParametersByReportId { get; set; } = [];
}

public sealed class ExportLocationBasePathOptionViewModel
{
    public string Label { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}

public sealed class ScheduledJobParameterInputViewModel
{
    public string Name { get; set; } = string.Empty;
    public string QueryKey { get; set; } = string.Empty;
    public string? DefaultValue { get; set; }
    public bool IsRequired { get; set; }
}

public sealed class ReportDefinitionLookupItem
{
    public int Id { get; set; }
    public string ReportName { get; set; } = string.Empty;
}
