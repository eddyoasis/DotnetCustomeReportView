using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataWarehousePower.Models
{
    [Table("AuditLogs")]
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

        [MaxLength(1024)]
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

        public DateTime TimestampUtc { get; set; }
    }
}