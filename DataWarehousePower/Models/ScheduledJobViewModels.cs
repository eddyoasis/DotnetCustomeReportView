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
    public string CronExpression { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public sealed class ScheduledJobListViewModel
{
    public List<ScheduledJobListItemViewModel> Jobs { get; set; } = [];
}

public sealed class ScheduledJobFormViewModel
{
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
    [MaxLength(128)]
    [Display(Name = "Cron Expression")]
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

    [Display(Name = "Enabled")]
    public bool IsActive { get; set; } = true;

    public List<ReportDefinitionLookupItem> AvailableReports { get; set; } = [];
}

public sealed class ReportDefinitionLookupItem
{
    public int Id { get; set; }
    public string ReportName { get; set; } = string.Empty;
}
