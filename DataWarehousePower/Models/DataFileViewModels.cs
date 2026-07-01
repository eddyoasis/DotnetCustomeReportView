namespace DataWarehousePower.Models
{
    public sealed class DataFileBrowserViewModel
    {
        public DataFileManageFilterViewModel Filter { get; set; } = new();
        public List<DataFileDefinition> DataFiles { get; set; } = new();
        public DataFileDefinition? SelectedDataFile { get; set; }
    }
}