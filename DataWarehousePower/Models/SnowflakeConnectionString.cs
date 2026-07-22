using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataWarehousePower.Models
{
    [Table("TBL_SnowflakeConnectionStrings")]
    public class SnowflakeConnectionString
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        public string ConnectionString { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        [MaxLength(50)]
        public string? ModifiedBy { get; set; }

        public DateTime? ModifiedAt { get; set; }
    }
}
