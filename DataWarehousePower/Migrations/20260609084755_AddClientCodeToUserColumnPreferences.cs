using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataWarehousePower.Migrations
{
    /// <inheritdoc />
    public partial class AddClientCodeToUserColumnPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_UserColumnPreferences",
                table: "UserColumnPreferences");

            migrationBuilder.AddColumn<string>(
                name: "ClientCode",
                table: "UserColumnPreferences",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "")
                .Annotation("Relational:ColumnOrder", 2);

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserColumnPreferences",
                table: "UserColumnPreferences",
                columns: new[] { "UserId", "ReportDefinitionId", "ClientCode" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_UserColumnPreferences",
                table: "UserColumnPreferences");

            migrationBuilder.DropColumn(
                name: "ClientCode",
                table: "UserColumnPreferences");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserColumnPreferences",
                table: "UserColumnPreferences",
                columns: new[] { "UserId", "ReportDefinitionId" });
        }
    }
}
