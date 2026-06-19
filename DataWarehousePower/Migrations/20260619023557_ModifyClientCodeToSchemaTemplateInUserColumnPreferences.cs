using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataWarehousePower.Migrations
{
    /// <inheritdoc />
    public partial class ModifyClientCodeToSchemaTemplateInUserColumnPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ClientCode",
                table: "TBL_UserColumnPreferences",
                newName: "SchemaTemplate");

            migrationBuilder.RenameIndex(
                name: "IX_TBL_UserColumnPreferences_UserId_ReportDefinitionId_ClientCode",
                table: "TBL_UserColumnPreferences",
                newName: "IX_TBL_UserColumnPreferences_UserId_ReportDefinitionId_SchemaTemplate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SchemaTemplate",
                table: "TBL_UserColumnPreferences",
                newName: "ClientCode");

            migrationBuilder.RenameIndex(
                name: "IX_TBL_UserColumnPreferences_UserId_ReportDefinitionId_SchemaTemplate",
                table: "TBL_UserColumnPreferences",
                newName: "IX_TBL_UserColumnPreferences_UserId_ReportDefinitionId_ClientCode");
        }
    }
}
