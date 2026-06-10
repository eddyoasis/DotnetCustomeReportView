namespace DataWarehousePower.Models
{
    /// <summary>
    /// Generic view model used by every report — driven entirely by DB config.
    /// </summary>
    public class ReportViewModel
    {
        public int    ReportId   { get; set; }
        public string ReportName { get; set; } = string.Empty;
        public string ClientCode { get; set; } = string.Empty;
        public List<string> AvailableClientCodes { get; set; } = new();

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
