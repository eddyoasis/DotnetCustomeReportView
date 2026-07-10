using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataWarehousePower.Models
{
    [Table("TBL_DW_Scheme")]

    public class DWScheme
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string SP { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Display { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string FilterDateColumnName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string InsertedBy { get; set; } = string.Empty;

        public DateTime InsertedDatetime { get; set; }

        [MaxLength(50)]
        public string? ModifiedBy { get; set; }

        public DateTime? ModifiedDatetime { get; set; }
    }
}
