namespace DataWarehousePower.Models.AppSettings
{
    public sealed class ClientCodeLookupOptions
    {
        public const string SectionName = "ClientCodeLookup";

        public string GetTRsStoredProcedureName { get; set; } = "usp_getTRIDsByUserId";
        public string GetCCsStoredProcedureName { get; set; } = "usp_getClientCodesByUserId";

        public string UserIdParameterName { get; set; } = "@UserId";
        public string TableNameParameterName { get; set; } = "@tableName";

        public string ResponseColumnName { get; set; } = "ClientCode";
    }
}