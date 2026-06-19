namespace DataWarehousePower.Models
{
    /// <summary>
    /// Generic view model used by every report — driven entirely by DB config.
    /// </summary>
    public class ReportViewModel
    {
        public int    ReportId   { get; set; }
        public string ReportName { get; set; } = string.Empty;
        public string SchemaTemplate { get; set; } = string.Empty;
        public int? ActivePreferenceId { get; set; }
        public string ClientCode { get; set; } = string.Empty;
        public DateTime? FilterDateFrom { get; set; }
        public DateTime? FilterDateTo { get; set; }
        public List<string> AvailableSchemaTemplates { get; set; } = new();
        public Dictionary<string, int> SchemaTemplatePreferenceIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<int, List<string>> ReportSchemaTemplatesByReportId { get; set; } = new();

        /// <summary>All columns defined for this report (from ReportColumns table).</summary>
        public List<ColumnDefinition> AvailableColumns { get; set; } = new();

        /// <summary>Ordered + filtered columns after applying user preferences.</summary>
        public List<ColumnDefinition> DisplayColumns { get; set; } = new();

        /// <summary>
        /// Dynamic rows — each row is a dictionary of { columnName → value }.
        /// </summary>
        public List<Dictionary<string, object?>> Rows { get; set; } = new();

        /// <summary>All registered reports for the navigation sidebar.</summary>
        public List<ReportDefinition> AllReports { get; set; } = new();
    }
}
