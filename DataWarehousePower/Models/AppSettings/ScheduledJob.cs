namespace DataWarehousePower.Models.AppSettings
{
    public class ScheduledJob
    {
        public ExportSplit ExportSplit { get; set; }
    }

    public class ExportSplit
    {
        public int MaxExportRecord { get; set; } = 200000;
        public int MaxTotalRecord { get; set; } = 1000000;
    }
}