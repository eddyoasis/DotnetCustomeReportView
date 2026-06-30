using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataWarehousePower.Migrations
{
    /// <inheritdoc />
    public partial class AddDataFileForeignKeyToScheduledReportJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "ReportDefinitionId",
                table: "TBL_ScheduledReportJobs",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "DataFileDefinitionId",
                table: "TBL_ScheduledReportJobs",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TBL_ScheduledReportJobs_DataFileDefinitionId",
                table: "TBL_ScheduledReportJobs",
                column: "DataFileDefinitionId");

            migrationBuilder.AddForeignKey(
                name: "FK_TBL_ScheduledReportJobs_TBL_DataFileDefinitions_DataFileDefinitionId",
                table: "TBL_ScheduledReportJobs",
                column: "DataFileDefinitionId",
                principalTable: "TBL_DataFileDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TBL_ScheduledReportJobs_TBL_DataFileDefinitions_DataFileDefinitionId",
                table: "TBL_ScheduledReportJobs");

            migrationBuilder.DropIndex(
                name: "IX_TBL_ScheduledReportJobs_DataFileDefinitionId",
                table: "TBL_ScheduledReportJobs");

            migrationBuilder.DropColumn(
                name: "DataFileDefinitionId",
                table: "TBL_ScheduledReportJobs");

            migrationBuilder.AlterColumn<int>(
                name: "ReportDefinitionId",
                table: "TBL_ScheduledReportJobs",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
