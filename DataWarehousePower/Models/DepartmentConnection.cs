using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataWarehousePower.Models
{
    /// <summary>
    /// A column belonging to a ReportDefinition.
    /// PropertyName must match the exact column name in the SourceTable.
    /// </summary>
    [Table("TBL_DepartmentConnections")]
    public class DepartmentConnection
    {
        [Key]
        public int Id { get; set; }

        public int DepartmentId { get; set; }

        [ForeignKey(nameof(DepartmentId))]
        public Department? Department { get; set; }

        public int ReportConnectionStringId { get; set; }

        [ForeignKey(nameof(ReportConnectionStringId))]
        public ReportConnectionString? ReportConnectionString { get; set; }

        [Required]
        [MaxLength(50)]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        [MaxLength(50)]
        public string? ModifiedBy { get; set; }

        public DateTime? ModifiedAt { get; set; }
    }
}
