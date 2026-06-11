using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataWarehousePower.Migrations
{
    /// <inheritdoc />
    public partial class AddIdPrimaryKeyToUserColumnPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_UserColumnPreferences",
                table: "UserColumnPreferences");

            migrationBuilder.AlterColumn<string>(
                name: "ClientCode",
                table: "UserColumnPreferences",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldDefaultValue: "")
                .OldAnnotation("Relational:ColumnOrder", 2);

            migrationBuilder.AlterColumn<int>(
                name: "ReportDefinitionId",
                table: "UserColumnPreferences",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .OldAnnotation("Relational:ColumnOrder", 1);

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "UserColumnPreferences",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128)
                .OldAnnotation("Relational:ColumnOrder", 0);

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "UserColumnPreferences",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserColumnPreferences",
                table: "UserColumnPreferences",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_UserColumnPreferences_UserId_ReportDefinitionId_ClientCode",
                table: "UserColumnPreferences",
                columns: new[] { "UserId", "ReportDefinitionId", "ClientCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_UserColumnPreferences",
                table: "UserColumnPreferences");

            migrationBuilder.DropIndex(
                name: "IX_UserColumnPreferences_UserId_ReportDefinitionId_ClientCode",
                table: "UserColumnPreferences");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "UserColumnPreferences");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "UserColumnPreferences",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128)
                .Annotation("Relational:ColumnOrder", 0);

            migrationBuilder.AlterColumn<int>(
                name: "ReportDefinitionId",
                table: "UserColumnPreferences",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("Relational:ColumnOrder", 1);

            migrationBuilder.AlterColumn<string>(
                name: "ClientCode",
                table: "UserColumnPreferences",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldDefaultValue: "")
                .Annotation("Relational:ColumnOrder", 2);

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserColumnPreferences",
                table: "UserColumnPreferences",
                columns: new[] { "UserId", "ReportDefinitionId", "ClientCode" });
        }
    }
}
