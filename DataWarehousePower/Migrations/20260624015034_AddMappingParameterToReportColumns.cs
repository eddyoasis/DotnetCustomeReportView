using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataWarehousePower.Migrations
{
    /// <inheritdoc />
    public partial class AddMappingParameterToReportColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MappingParameter",
                table: "TBL_ReportColumns",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "TBL_ReportColumns",
                keyColumn: "Id",
                keyValue: 1,
                column: "MappingParameter",
                value: null);

            migrationBuilder.UpdateData(
                table: "TBL_ReportColumns",
                keyColumn: "Id",
                keyValue: 2,
                column: "MappingParameter",
                value: null);

            migrationBuilder.UpdateData(
                table: "TBL_ReportColumns",
                keyColumn: "Id",
                keyValue: 3,
                column: "MappingParameter",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MappingParameter",
                table: "TBL_ReportColumns");
        }
    }
}
