namespace DataWarehousePower.Models
{
    public class DataFilePreviewColumnRequest
    {
        public string PropertyName { get; set; } = string.Empty;
        public string? MappingParameter { get; set; }
        public string? MappingParameterFilter { get; set; }
    }

    public class DataFilePreviewRequest
    {
        public string? UserId { get; set; }
        public string? SourceDatabase { get; set; }
        public string? SourceTable { get; set; }
        public string? SourceSP { get; set; }
        public string? ClientCode { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public Dictionary<string, string?>? Parameters { get; set; }
        public int Page { get; set; } = 1;
        public int Take { get; set; } = 10;
        public bool IsExport { get; set; } = false;
        public List<DataFilePreviewColumnRequest> Columns { get; set; } = new();
    }

    public class DataFilePreviewResult
    {
        public int TotalRowCount { get; set; }
        public List<string> Columns { get; set; } = new();
        public List<Dictionary<string, object?>> Rows { get; set; } = new();
    }
}
