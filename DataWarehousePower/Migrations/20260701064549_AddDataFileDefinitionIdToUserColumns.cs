using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataWarehousePower.Migrations
{
    /// <inheritdoc />
    public partial class AddDataFileDefinitionIdToUserColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TBL_UserColumnPreferences_UserId_ReportDefinitionId_SchemaTemplate",
                table: "TBL_UserColumnPreferences");

            migrationBuilder.AddColumn<int>(
                name: "DataFileDefinitionId",
                table: "TBL_UserColumnPreferences",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_TBL_UserColumnPreferences_UserId_DataFileDefinitionId_SchemaTemplate",
                table: "TBL_UserColumnPreferences",
                columns: new[] { "UserId", "DataFileDefinitionId", "SchemaTemplate" },
                unique: true,
                filter: "[DataFileDefinitionId] <> 0");

            migrationBuilder.CreateIndex(
                name: "IX_TBL_UserColumnPreferences_UserId_ReportDefinitionId_SchemaTemplate",
                table: "TBL_UserColumnPreferences",
                columns: new[] { "UserId", "ReportDefinitionId", "SchemaTemplate" },
                unique: true,
                filter: "[ReportDefinitionId] <> 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TBL_UserColumnPreferences_UserId_DataFileDefinitionId_SchemaTemplate",
                table: "TBL_UserColumnPreferences");

            migrationBuilder.DropIndex(
                name: "IX_TBL_UserColumnPreferences_UserId_ReportDefinitionId_SchemaTemplate",
                table: "TBL_UserColumnPreferences");

            migrationBuilder.DropColumn(
                name: "DataFileDefinitionId",
                table: "TBL_UserColumnPreferences");

            migrationBuilder.CreateIndex(
                name: "IX_TBL_UserColumnPreferences_UserId_ReportDefinitionId_SchemaTemplate",
                table: "TBL_UserColumnPreferences",
                columns: new[] { "UserId", "ReportDefinitionId", "SchemaTemplate" },
                unique: true);
        }
    }
}
