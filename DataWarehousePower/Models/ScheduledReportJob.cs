using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataWarehousePower.Models;

[Table("TBL_ScheduledReportJobs")]
public class ScheduledReportJob
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(128)]
    public string JobName { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string HangfireJobId { get; set; } = string.Empty;

    public int? ReportDefinitionId { get; set; }

    public int? DataFileDefinitionId { get; set; }

    [Required]
    [MaxLength(16)]
    public string Format { get; set; } = "csv";

    [Required]
    [MaxLength(128)]
    public string CronExpression { get; set; } = "0 8 * * *";

    [Required]
    [MaxLength(64)]
    public string JobAction { get; set; } = ScheduledJobActions.ExportFile;

    [MaxLength(2048)]
    public string? RecipientEmail { get; set; }

    [MaxLength(128)]
    public string? SchemaTemplate { get; set; }

    [MaxLength(128)]
    public string? ClientCode { get; set; }

    public string? Parameters { get; set; }

    public string? RecurringDataDateColumn { get; set; }

    public string? FilterColumnDataJson { get; set; }

    public bool IsExportToClientFolder { get; set; } = false;

    [MaxLength(512)]
    public string? ExportLocation { get; set; }

    public bool ExportToLocalFolder { get; set; }

    public DateTime? DateFrom { get; set; }

    public DateTime? DateTo { get; set; }

    [MaxLength(16)]
    public string? AutoDateIntervalUnit { get; set; }

    public int? AutoDateIntervalValue { get; set; }

    public bool IsCustom { get; set; } = true;

    [Required]
    [MaxLength(512)]
    public string EncryptedPassword { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    [Required]
    [MaxLength(128)]
    public string CreatedByUserId { get; set; } = "system";

    [Required]
    [MaxLength(128)]
    public string CreatedByUsername { get; set; } = "System";

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(128)]
    public string? UpdatedByUserId { get; set; }

    [MaxLength(128)]
    public string? UpdatedByUsername { get; set; }

    public DateTime? UpdatedUtc { get; set; }

    [ForeignKey(nameof(ReportDefinitionId))]
    public ReportDefinition? ReportDefinition { get; set; }

    [ForeignKey(nameof(DataFileDefinitionId))]
    public DataFileDefinition? DataFileDefinition { get; set; }
}
