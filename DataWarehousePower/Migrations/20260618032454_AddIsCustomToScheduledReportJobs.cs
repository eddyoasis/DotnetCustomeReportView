using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataWarehousePower.Migrations
{
    /// <inheritdoc />
    public partial class AddIsCustomToScheduledReportJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "RecipientEmail",
                table: "TBL_ScheduledReportJobs",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCustom",
                table: "TBL_ScheduledReportJobs",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsCustom",
                table: "TBL_ScheduledReportJobs");

            migrationBuilder.AlterColumn<string>(
                name: "RecipientEmail",
                table: "TBL_ScheduledReportJobs",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2048)",
                oldMaxLength: 2048,
                oldNullable: true);
        }
    }
}
