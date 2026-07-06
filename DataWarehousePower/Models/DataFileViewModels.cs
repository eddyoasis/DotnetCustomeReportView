namespace DataWarehousePower.Models
{
    public sealed class DataFileBrowserViewModel
    {
        public DataFileManageFilterViewModel Filter { get; set; } = new();
        public List<DataFileDefinition> DataFiles { get; set; } = new();
        public DataFileDefinition? SelectedDataFile { get; set; }
        public DateTime? FilterDateFrom { get; set; }
        public DateTime? FilterDateTo { get; set; }
        public string SchemaTemplate { get; set; } = string.Empty;
        public int? ActivePreferenceId { get; set; }
        public List<string> AvailableSchemaTemplates { get; set; } = new();
        public Dictionary<string, int> SchemaTemplatePreferenceIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<ColumnDefinition> AvailableColumns { get; set; } = new();
        public List<ColumnDefinition> DisplayColumns { get; set; } = new();
    }
}