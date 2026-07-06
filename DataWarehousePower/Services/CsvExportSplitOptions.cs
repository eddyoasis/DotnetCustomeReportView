namespace DataWarehousePower.Services
{
    // Split thresholds used for scheduled export files (CSV/Excel/PDF).
    public sealed class CsvExportSplitOptions
    {
        public int? MaxRowsPerFile { get; init; }
        public long? MaxBytesPerFile { get; init; }
    }
}
