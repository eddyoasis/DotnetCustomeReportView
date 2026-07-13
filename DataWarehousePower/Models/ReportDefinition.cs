using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataWarehousePower.Models
{
    /// <summary>
    /// Defines a report. Set either SourceTable OR SourceSP — not both.
    /// Add a row here to register a new report — no code changes needed.
    /// </summary>
    [Table("TBL_ReportDefinitions")]
    public class ReportDefinition
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Human-readable report name shown in the UI (e.g. "Staff Report").</summary>
        [Required]
        [MaxLength(200)]
        public string ReportName { get; set; } = string.Empty;

        /// <summary>
        /// Direct table query mode: the exact database name that contains the table.
        /// Leave null when not needed.
        /// </summary>
        [MaxLength(200)]
        public string? SourceDatabase { get; set; }

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

        /// <summary>
        /// JSON configuration for source parameters, including optional default values.
        /// Example: [{"Name":"@RegionCode","DefaultValue":"NA","MappingParameter":"region"}]
        /// </summary>
        public string? Parameters { get; set; }

        /// <summary>
        /// When false the report is hidden from the report selection list.
        /// Defaults to true so all new reports are immediately visible.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Comma-separated department names allowed to view this report
        /// (e.g. "Sales,HR,Finance"). Empty means visible to all departments.
        /// </summary>
        [MaxLength(1000)]
        public string? Departments { get; set; }

        [MaxLength(50)]
        public string? FilterClientCodeColumn { get; set; }

        [MaxLength(50)]
        public string? FilterDateColumn { get; set; }

        // Navigation
        public ICollection<ReportColumn> Columns { get; set; } = new List<ReportColumn>();
    }
}
