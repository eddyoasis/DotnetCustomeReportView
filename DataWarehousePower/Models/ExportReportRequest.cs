namespace DataWarehousePower.Models
{
    public class ExportReportRequest
    {
        // Backward-compatible single format field for older clients.
        public string? Format { get; set; }
        public List<string>? Formats { get; set; }
        public string Password { get; set; } = string.Empty;
        public string? SchemaTemplate { get; set; }
        public string? FilterSchemaTemplate { get; set; }

        // Backward compatibility for older clients still posting clientCode keys.
        public string? ClientCode
        {
            get => SchemaTemplate;
            set => SchemaTemplate = value;
        }

        public string? FilterClientCode
        {
            get => FilterSchemaTemplate;
            set => FilterSchemaTemplate = value;
        }

        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
    }
}
