using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataWarehousePower.Models
{
    [Table("TBL_AuditLogs")]
    public class AuditLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [MaxLength(128)]
        public string UserId { get; set; } = string.Empty;

        [MaxLength(128)]
        public string Username { get; set; } = string.Empty;

        [MaxLength(64)]
        public string ActionType { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        [MaxLength(128)]
        public string EntityName { get; set; } = string.Empty;

        [MaxLength(256)]
        public string? EntityId { get; set; }

        public string? OldValues { get; set; }
        public string? NewValues { get; set; }
        public string? ChangedColumns { get; set; }
        public string? Metadata { get; set; }

        [MaxLength(128)]
        public string? CorrelationId { get; set; }

        [MaxLength(64)]
        public string? IpAddress { get; set; }

        [MaxLength(256)]
        public string? Host { get; set; }

        [MaxLength(16)]
        public string? RequestMethod { get; set; }

        [MaxLength(2048)]
        public string? RequestPath { get; set; }

        [MaxLength(2048)]
        public string? QueryString { get; set; }

        [MaxLength(1024)]
        public string? UserAgent { get; set; }

        [MaxLength(1024)]
        public string? Referrer { get; set; }

        [MaxLength(16)]
        public string? Protocol { get; set; }

        public int? StatusCode { get; set; }

        [MaxLength(128)]
        public string? SessionId { get; set; }

        public long? DurationMs { get; set; }

        [Column("TimestampUtc")]
        public DateTime CreatedAt { get; set; }
    }
}