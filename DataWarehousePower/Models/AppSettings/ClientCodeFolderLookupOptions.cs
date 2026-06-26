namespace DataWarehousePower.Models.AppSettings
{
    public sealed class ClientCodeFolderLookupOptions
    {
        public const string SectionName = "ClientCodeFolderLookup";

        public string StoredProcedureName { get; set; } = "usp_getClientCodeFoldersByUserId";

        public string UserIdParameterName { get; set; } = "@UserId";

        public string ResponseColumnName { get; set; } = "ClientCodeFolder";
    }
}