using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataWarehousePower.Migrations
{
    /// <inheritdoc />
    public partial class AddExportLocationToScheduledReportJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExportLocation",
                table: "TBL_ScheduledReportJobs",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExportLocation",
                table: "TBL_ScheduledReportJobs");
        }
    }
}
