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

    public int ReportDefinitionId { get; set; }

    [Required]
    [MaxLength(16)]
    public string Format { get; set; } = "csv";

    [Required]
    [MaxLength(128)]
    public string CronExpression { get; set; } = "0 8 * * *";

    [MaxLength(128)]
    public string? ClientCode { get; set; }

    [MaxLength(128)]
    public string? FilterClientCode { get; set; }

    public DateTime? DateFrom { get; set; }

    public DateTime? DateTo { get; set; }

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
}
