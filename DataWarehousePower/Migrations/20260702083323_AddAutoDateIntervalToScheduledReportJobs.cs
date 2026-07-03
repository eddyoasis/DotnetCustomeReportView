using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataWarehousePower.Migrations
{
    /// <inheritdoc />
    public partial class AddAutoDateIntervalToScheduledReportJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AutoDateIntervalUnit",
                table: "TBL_ScheduledReportJobs",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AutoDateIntervalValue",
                table: "TBL_ScheduledReportJobs",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoDateIntervalUnit",
                table: "TBL_ScheduledReportJobs");

            migrationBuilder.DropColumn(
                name: "AutoDateIntervalValue",
                table: "TBL_ScheduledReportJobs");
        }
    }
}
