using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataWarehousePower.Models
{
    /// <summary>
    /// Defines a report: its friendly name and the DB table it reads from.
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
        /// The exact DB table name to SELECT from (e.g. "ReportStaff").
        /// Must be an existing table in the same database.
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string SourceTable { get; set; } = string.Empty;

        // Navigation
        public ICollection<ReportColumn> Columns { get; set; } = new List<ReportColumn>();
    }
}
