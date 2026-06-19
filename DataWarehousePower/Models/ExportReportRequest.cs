namespace DataWarehousePower.Models
{
    public class ExportReportRequest
    {
        // Backward-compatible single format field for older clients.
        public string? Format { get; set; }
        public List<string>? Formats { get; set; }
        public string Password { get; set; } = string.Empty;
        public string? ClientCode { get; set; }
        public string? FilterClientCode { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
    }
}
