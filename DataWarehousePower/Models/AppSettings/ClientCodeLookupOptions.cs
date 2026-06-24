namespace DataWarehousePower.Models.AppSettings
{
    public sealed class ClientCodeLookupOptions
    {
        public const string SectionName = "ClientCodeLookup";

        public string StoredProcedureName { get; set; } = "usp_getClientCodesByUserId";

        public string UserIdParameterName { get; set; } = "@UserId";

        public string ResponseColumnName { get; set; } = "ClientCode";
    }
}