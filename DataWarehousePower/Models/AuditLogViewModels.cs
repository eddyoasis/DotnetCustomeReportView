namespace DataWarehousePower.Models
{
    public class AuditLogFilterViewModel
    {
        public string? UserId { get; set; }
        public string? ActionType { get; set; }
        public string? EntityName { get; set; }
        public string? IpAddress { get; set; }
        public string? Host { get; set; }
        public DateTime? DateFromUtc { get; set; }
        public DateTime? DateToUtc { get; set; }
        public string? Search { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 25;
    }

    public class AuditLogListItemViewModel
    {
        public long Id { get; set; }
        public DateTime TimestampUtc { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string EntityName { get; set; } = string.Empty;
        public string? EntityId { get; set; }
        public string? IpAddress { get; set; }
        public string? Host { get; set; }
        public string? RequestMethod { get; set; }
        public string? RequestPath { get; set; }
        public string? ChangedColumns { get; set; }
        public string? OldValues { get; set; }
        public string? NewValues { get; set; }
        public string? Metadata { get; set; }
    }

    public class AuditLogListViewModel
    {
        public AuditLogFilterViewModel Filter { get; set; } = new();
        public List<AuditLogListItemViewModel> Items { get; set; } = new();
        public List<string> AvailableUsers { get; set; } = new();
        public List<string> AvailableActionTypes { get; set; } = new();
        public List<string> AvailableEntityNames { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
    }

    public class AuditLogFieldComparisonViewModel
    {
        public string FieldName { get; set; } = string.Empty;
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public bool IsChanged { get; set; }
    }

    public class AuditLogDetailViewModel
    {
        public long Id { get; set; }
        public DateTime TimestampUtc { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string EntityName { get; set; } = string.Empty;
        public string? EntityId { get; set; }
        public string? CorrelationId { get; set; }
        public string? IpAddress { get; set; }
        public string? Host { get; set; }
        public string? RequestMethod { get; set; }
        public string? RequestPath { get; set; }
        public string? QueryString { get; set; }
        public string? UserAgent { get; set; }
        public string? Referrer { get; set; }
        public string? Protocol { get; set; }
        public int? StatusCode { get; set; }
        public string? SessionId { get; set; }
        public long? DurationMs { get; set; }
        public string? Metadata { get; set; }
        public string? ChangedColumns { get; set; }
        public string? OldValuesJson { get; set; }
        public string? NewValuesJson { get; set; }
        public List<AuditLogFieldComparisonViewModel> Comparisons { get; set; } = new();
        public Dictionary<string, string?> OldValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string?> NewValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> ChangedColumnsList { get; set; } = new();
        public bool IsUpdateAction { get; set; }
        public bool HasComparisonData => Comparisons.Count > 0;
    }
}
