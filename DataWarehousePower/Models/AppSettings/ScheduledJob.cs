namespace DataWarehousePower.Models.AppSettings
{
    public class ScheduledJob
    {
        public ExportSplit ExportSplit { get; set; }
    }

    public class ExportSplit
    {
        public int MaxTotalRecord { get; set; } = 100000;
    }
}