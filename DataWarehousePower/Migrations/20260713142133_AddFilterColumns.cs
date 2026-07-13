using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataWarehousePower.Migrations
{
    /// <inheritdoc />
    public partial class AddFilterColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "TBL_ReportDefinitions",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "FilterClientCodeColumn",
                table: "TBL_ReportDefinitions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FilterDateColumn",
                table: "TBL_ReportDefinitions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FilterClientCodeColumn",
                table: "TBL_DataFileDefinitions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FilterDateColumn",
                table: "TBL_DataFileDefinitions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FilterClientCodeColumn",
                table: "TBL_ReportDefinitions");

            migrationBuilder.DropColumn(
                name: "FilterDateColumn",
                table: "TBL_ReportDefinitions");

            migrationBuilder.DropColumn(
                name: "FilterClientCodeColumn",
                table: "TBL_DataFileDefinitions");

            migrationBuilder.DropColumn(
                name: "FilterDateColumn",
                table: "TBL_DataFileDefinitions");

            migrationBuilder.InsertData(
                table: "TBL_ReportDefinitions",
                columns: new[] { "Id", "Departments", "IsActive", "Parameters", "ReportName", "SourceDatabase", "SourceSP", "SourceTable" },
                values: new object[] { 1, null, true, null, "Staff Report", null, null, "ReportStaff" });
        }
    }
}
