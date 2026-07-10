namespace DataWarehousePower.Models
{
    /// <summary>
    /// View model passed to the Index view.
    /// </summary>
    public class ReportStaffViewModel
    {
        /// <summary>All available columns (system-defined, with default labels).</summary>
        public List<ColumnDefinition> AvailableColumns { get; set; } = new()
        {
            new ColumnDefinition { Key = "Id",   DefaultLabel = "ID",   DisplayLabel = "ID"   },
            new ColumnDefinition { Key = "Name", DefaultLabel = "Name", DisplayLabel = "Name" },
            new ColumnDefinition { Key = "Age",  DefaultLabel = "Age",  DisplayLabel = "Age"  }
        };

        /// <summary>
        /// Ordered list of column definitions the user wants to see,
        /// including their custom display labels and visibility.
        /// </summary>
        public List<ColumnDefinition> DisplayColumns { get; set; } = new();

        /// <summary>The data rows to display.</summary>
        public List<ReportStaff> Data { get; set; } = new();
    }

    public class ColumnDefinition
    {
        public string Key          { get; set; } = string.Empty;
        public string DefaultLabel { get; set; } = string.Empty;
        /// <summary>User-customised display label. Falls back to DefaultLabel if not set.</summary>
        public string DisplayLabel { get; set; } = string.Empty;
        public bool   IsVisible    { get; set; } = true;
        public int    Order        { get; set; } = 0;

        public string PropertyName { get; set; } = string.Empty;
        public string PropertyType { get; set; } = string.Empty;
        public string? MappingParameter { get; set; }
        public string? MappingParameterFilter { get; set; }
    }
}
