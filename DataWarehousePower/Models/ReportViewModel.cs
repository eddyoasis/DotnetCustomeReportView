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
        public bool HasAppliedFilters { get; set; }
        public List<string> AvailableClientCodes { get; set; } = new();
        public DateTime? FilterDateFrom { get; set; }
        public DateTime? FilterDateTo { get; set; }
        public List<ReportRuntimeParameter> RuntimeParameters { get; set; } = new();
        public bool HasMissingRequiredParameters { get; set; }
        public Dictionary<string, string> ActiveParameterValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);
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

        /// <summary>Current page number (1-based).</summary>
        public int Page { get; set; } = 1;

        /// <summary>Number of records per page.</summary>
        public int PageSize { get; set; } = 50;

        /// <summary>Total number of records before pagination.</summary>
        public int TotalRows { get; set; }

        /// <summary>Total number of pages.</summary>
        public int TotalPages { get; set; } = 1;

        public int StartRow => TotalRows == 0 ? 0 : ((Page - 1) * PageSize) + 1;

        public int EndRow => Math.Min(Page * PageSize, TotalRows);

        /// <summary>All registered reports for the navigation sidebar.</summary>
        public List<ReportDefinition> AllReports { get; set; } = new();
        public bool CanExport { get; set; } = false;
    }

    public class ReportRuntimeParameter
    {
        public string Name { get; set; } = string.Empty;
        public string QueryKey { get; set; } = string.Empty;
        public string? DefaultValue { get; set; }
        public string? Value { get; set; }
        public bool IsRequired { get; set; } = true;
    }
}
