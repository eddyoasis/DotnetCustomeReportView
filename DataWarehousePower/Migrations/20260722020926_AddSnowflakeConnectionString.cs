using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataWarehousePower.Migrations
{
    /// <inheritdoc />
    public partial class AddSnowflakeConnectionString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FilterColumnDataJson",
                table: "TBL_ScheduledReportJobs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecurringDataDateColumn",
                table: "TBL_ScheduledReportJobs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TBL_SnowflakeConnectionStrings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConnectionString = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TBL_SnowflakeConnectionStrings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TBL_SnowflakeConnectionStrings");

            migrationBuilder.DropColumn(
                name: "FilterColumnDataJson",
                table: "TBL_ScheduledReportJobs");

            migrationBuilder.DropColumn(
                name: "RecurringDataDateColumn",
                table: "TBL_ScheduledReportJobs");
        }
    }
}
