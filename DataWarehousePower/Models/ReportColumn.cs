using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataWarehousePower.Models
{
    /// <summary>
    /// A column belonging to a ReportDefinition.
    /// PropertyName must match the exact column name in the SourceTable.
    /// </summary>
    [Table("TBL_ReportColumns")]
    public class ReportColumn
    {
        [Key]
        public int Id { get; set; }

        public int ReportDefinitionId { get; set; }

        [ForeignKey(nameof(ReportDefinitionId))]
        public ReportDefinition? Report { get; set; }

        /// <summary>Exact column name in the source DB table (e.g. "Age").</summary>
        [Required]
        [MaxLength(100)]
        public string PropertyName { get; set; } = string.Empty;

        /// <summary>Default header label shown in the UI (e.g. "Age").</summary>
        [Required]
        [MaxLength(100)]
        public string DefaultLabel { get; set; } = string.Empty;

        /// <summary>Default display order (1-based).</summary>
        public int DisplayOrder { get; set; } = 1;
    }
}
