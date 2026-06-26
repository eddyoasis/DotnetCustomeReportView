namespace DataWarehousePower.Models.AppSettings
{
    public sealed class RequestAuditLoggingOptions
    {
        public const string SectionName = "RequestAuditLogging";

        public List<string> ExcludedEntityNames { get; set; } = [];
        public List<string> ExcludedActionTypes { get; set; } = [];
        public List<string> ExcludedPathPrefixes { get; set; } = [];
    }
}