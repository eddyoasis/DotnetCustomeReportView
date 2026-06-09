using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataWarehousePower.Models
{
    /// <summary>
    /// Defines a report. Set either SourceTable OR SourceSP — not both.
    /// Add a row here to register a new report — no code changes needed.
    /// </summary>
    [Table("ReportDefinitions")]
    public class ReportDefinition
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Human-readable report name shown in the UI (e.g. "Staff Report").</summary>
        [Required]
        [MaxLength(200)]
        public string ReportName { get; set; } = string.Empty;

        /// <summary>
        /// Direct table query mode: the exact DB table name to SELECT from.
        /// Leave null when using SourceSP instead.
        /// </summary>
        [MaxLength(200)]
        public string? SourceTable { get; set; }

        /// <summary>
        /// Stored procedure mode: the exact SP name to EXEC.
        /// Leave null when using SourceTable instead.
        /// When set, the engine calls EXEC [SourceSP] and maps results by column name.
        /// </summary>
        [MaxLength(200)]
        public string? SourceSP { get; set; }

        // Navigation
        public ICollection<ReportColumn> Columns { get; set; } = new List<ReportColumn>();
    }
}
