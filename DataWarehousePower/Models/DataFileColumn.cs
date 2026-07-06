using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataWarehousePower.Models
{
    [Table("TBL_DataFileColumns")]
    public class DataFileColumn
    {
        [Key]
        public int Id { get; set; }

        public int DataFileDefinitionId { get; set; }

        [ForeignKey(nameof(DataFileDefinitionId))]
        public DataFileDefinition? DataFile { get; set; }

        [Required]
        [MaxLength(100)]
        public string PropertyName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string DefaultLabel { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? MappingParameter { get; set; }

        [MaxLength(200)]
        public string? MappingParameterFilter { get; set; }

        public int DisplayOrder { get; set; } = 1;
    }
}