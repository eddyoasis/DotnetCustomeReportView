using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataWarehousePower.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledReportJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReportColumns_ReportDefinitions_ReportDefinitionId",
                table: "ReportColumns");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserColumnPreferences",
                table: "UserColumnPreferences");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ReportDefinitions",
                table: "ReportDefinitions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ReportColumns",
                table: "ReportColumns");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AuditLogs",
                table: "AuditLogs");

            migrationBuilder.RenameTable(
                name: "UserColumnPreferences",
                newName: "TBL_UserColumnPreferences");

            migrationBuilder.RenameTable(
                name: "ReportDefinitions",
                newName: "TBL_ReportDefinitions");

            migrationBuilder.RenameTable(
                name: "ReportColumns",
                newName: "TBL_ReportColumns");

            migrationBuilder.RenameTable(
                name: "AuditLogs",
                newName: "TBL_AuditLogs");

            migrationBuilder.RenameIndex(
                name: "IX_UserColumnPreferences_UserId_ReportDefinitionId_ClientCode",
                table: "TBL_UserColumnPreferences",
                newName: "IX_TBL_UserColumnPreferences_UserId_ReportDefinitionId_ClientCode");

            migrationBuilder.RenameIndex(
                name: "IX_UserColumnPreferences_UserId",
                table: "TBL_UserColumnPreferences",
                newName: "IX_TBL_UserColumnPreferences_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_ReportColumns_ReportDefinitionId",
                table: "TBL_ReportColumns",
                newName: "IX_TBL_ReportColumns_ReportDefinitionId");

            migrationBuilder.RenameIndex(
                name: "IX_AuditLogs_UserId",
                table: "TBL_AuditLogs",
                newName: "IX_TBL_AuditLogs_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_AuditLogs_TimestampUtc",
                table: "TBL_AuditLogs",
                newName: "IX_TBL_AuditLogs_TimestampUtc");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TBL_UserColumnPreferences",
                table: "TBL_UserColumnPreferences",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TBL_ReportDefinitions",
                table: "TBL_ReportDefinitions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TBL_ReportColumns",
                table: "TBL_ReportColumns",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TBL_AuditLogs",
                table: "TBL_AuditLogs",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "TBL_ScheduledReportJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    HangfireJobId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ReportDefinitionId = table.Column<int>(type: "int", nullable: false),
                    Format = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CronExpression = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ClientCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    FilterClientCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    DateFrom = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DateTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EncryptedPassword = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedByUsername = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    UpdatedByUsername = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TBL_ScheduledReportJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TBL_ScheduledReportJobs_TBL_ReportDefinitions_ReportDefinitionId",
                        column: x => x.ReportDefinitionId,
                        principalTable: "TBL_ReportDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TBL_ScheduledReportJobs_HangfireJobId",
                table: "TBL_ScheduledReportJobs",
                column: "HangfireJobId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TBL_ScheduledReportJobs_ReportDefinitionId",
                table: "TBL_ScheduledReportJobs",
                column: "ReportDefinitionId");

            migrationBuilder.AddForeignKey(
                name: "FK_TBL_ReportColumns_TBL_ReportDefinitions_ReportDefinitionId",
                table: "TBL_ReportColumns",
                column: "ReportDefinitionId",
                principalTable: "TBL_ReportDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TBL_ReportColumns_TBL_ReportDefinitions_ReportDefinitionId",
                table: "TBL_ReportColumns");

            migrationBuilder.DropTable(
                name: "TBL_ScheduledReportJobs");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TBL_UserColumnPreferences",
                table: "TBL_UserColumnPreferences");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TBL_ReportDefinitions",
                table: "TBL_ReportDefinitions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TBL_ReportColumns",
                table: "TBL_ReportColumns");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TBL_AuditLogs",
                table: "TBL_AuditLogs");

            migrationBuilder.RenameTable(
                name: "TBL_UserColumnPreferences",
                newName: "UserColumnPreferences");

            migrationBuilder.RenameTable(
                name: "TBL_ReportDefinitions",
                newName: "ReportDefinitions");

            migrationBuilder.RenameTable(
                name: "TBL_ReportColumns",
                newName: "ReportColumns");

            migrationBuilder.RenameTable(
                name: "TBL_AuditLogs",
                newName: "AuditLogs");

            migrationBuilder.RenameIndex(
                name: "IX_TBL_UserColumnPreferences_UserId_ReportDefinitionId_ClientCode",
                table: "UserColumnPreferences",
                newName: "IX_UserColumnPreferences_UserId_ReportDefinitionId_ClientCode");

            migrationBuilder.RenameIndex(
                name: "IX_TBL_UserColumnPreferences_UserId",
                table: "UserColumnPreferences",
                newName: "IX_UserColumnPreferences_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_TBL_ReportColumns_ReportDefinitionId",
                table: "ReportColumns",
                newName: "IX_ReportColumns_ReportDefinitionId");

            migrationBuilder.RenameIndex(
                name: "IX_TBL_AuditLogs_UserId",
                table: "AuditLogs",
                newName: "IX_AuditLogs_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_TBL_AuditLogs_TimestampUtc",
                table: "AuditLogs",
                newName: "IX_AuditLogs_TimestampUtc");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserColumnPreferences",
                table: "UserColumnPreferences",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ReportDefinitions",
                table: "ReportDefinitions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ReportColumns",
                table: "ReportColumns",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AuditLogs",
                table: "AuditLogs",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ReportColumns_ReportDefinitions_ReportDefinitionId",
                table: "ReportColumns",
                column: "ReportDefinitionId",
                principalTable: "ReportDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
