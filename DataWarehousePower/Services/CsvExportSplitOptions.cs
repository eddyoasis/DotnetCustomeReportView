namespace DataWarehousePower.Services
{
    public sealed class CsvExportSplitOptions
    {
        public int? MaxRowsPerFile { get; init; }
        public long? MaxBytesPerFile { get; init; }
    }
}
