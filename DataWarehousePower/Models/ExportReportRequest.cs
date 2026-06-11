namespace DataWarehousePower.Models
{
    public class ExportReportRequest
    {
        public string Format { get; set; } = "csv";
        public string Password { get; set; } = string.Empty;
        public string? ClientCode { get; set; }
        public string? FilterClientCode { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
    }
}
