using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataWarehousePower.Migrations
{
    /// <inheritdoc />
    public partial class RenameFilterClientCodeToJobScheduler : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FilterClientCode",
                table: "TBL_ScheduledReportJobs",
                newName: "ClientCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ClientCode",
                table: "TBL_ScheduledReportJobs",
                newName: "FilterClientCode");
        }
    }
}
