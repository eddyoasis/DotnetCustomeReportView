using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataWarehousePower.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestContextFieldsToAuditLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "DurationMs",
                table: "TBL_AuditLogs",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Host",
                table: "TBL_AuditLogs",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IpAddress",
                table: "TBL_AuditLogs",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Protocol",
                table: "TBL_AuditLogs",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QueryString",
                table: "TBL_AuditLogs",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Referrer",
                table: "TBL_AuditLogs",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestMethod",
                table: "TBL_AuditLogs",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestPath",
                table: "TBL_AuditLogs",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SessionId",
                table: "TBL_AuditLogs",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StatusCode",
                table: "TBL_AuditLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserAgent",
                table: "TBL_AuditLogs",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DurationMs",
                table: "TBL_AuditLogs");

            migrationBuilder.DropColumn(
                name: "Host",
                table: "TBL_AuditLogs");

            migrationBuilder.DropColumn(
                name: "IpAddress",
                table: "TBL_AuditLogs");

            migrationBuilder.DropColumn(
                name: "Protocol",
                table: "TBL_AuditLogs");

            migrationBuilder.DropColumn(
                name: "QueryString",
                table: "TBL_AuditLogs");

            migrationBuilder.DropColumn(
                name: "Referrer",
                table: "TBL_AuditLogs");

            migrationBuilder.DropColumn(
                name: "RequestMethod",
                table: "TBL_AuditLogs");

            migrationBuilder.DropColumn(
                name: "RequestPath",
                table: "TBL_AuditLogs");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "TBL_AuditLogs");

            migrationBuilder.DropColumn(
                name: "StatusCode",
                table: "TBL_AuditLogs");

            migrationBuilder.DropColumn(
                name: "UserAgent",
                table: "TBL_AuditLogs");
        }
    }
}
