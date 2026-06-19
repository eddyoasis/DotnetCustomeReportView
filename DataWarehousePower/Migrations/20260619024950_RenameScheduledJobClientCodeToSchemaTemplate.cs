using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataWarehousePower.Migrations
{
    /// <inheritdoc />
    public partial class RenameScheduledJobClientCodeToSchemaTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ClientCode",
                table: "TBL_ScheduledReportJobs",
                newName: "SchemaTemplate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SchemaTemplate",
                table: "TBL_ScheduledReportJobs",
                newName: "ClientCode");
        }
    }
}
