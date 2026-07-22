using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataWarehousePower.Models
{
    [Table("TBL_DepartmentSnowflakeConnections")]
    public class DepartmentSnowflakeConnection
    {
        [Key]
        public int Id { get; set; }

        public int DepartmentId { get; set; }

        [ForeignKey(nameof(DepartmentId))]
        public Department? Department { get; set; }

        public int SnowflakeConnectionStringId { get; set; }

        [ForeignKey(nameof(SnowflakeConnectionStringId))]
        public SnowflakeConnectionString? SnowflakeConnectionString { get; set; }

        [Required]
        [MaxLength(50)]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        [MaxLength(50)]
        public string? ModifiedBy { get; set; }

        public DateTime? ModifiedAt { get; set; }
    }
}
