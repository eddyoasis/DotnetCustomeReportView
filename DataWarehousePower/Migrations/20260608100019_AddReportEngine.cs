using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DataWarehousePower.Migrations
{
    /// <inheritdoc />
    public partial class AddReportEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReportDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReportName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SourceTable = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserColumnPreferences",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ReportDefinitionId = table.Column<int>(type: "int", nullable: false),
                    ColumnJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserColumnPreferences", x => new { x.UserId, x.ReportDefinitionId });
                });

            migrationBuilder.CreateTable(
                name: "ReportColumns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReportDefinitionId = table.Column<int>(type: "int", nullable: false),
                    PropertyName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DefaultLabel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportColumns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportColumns_ReportDefinitions_ReportDefinitionId",
                        column: x => x.ReportDefinitionId,
                        principalTable: "ReportDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "ReportDefinitions",
                columns: new[] { "Id", "ReportName", "SourceTable" },
                values: new object[] { 1, "Staff Report", "ReportStaff" });

            migrationBuilder.InsertData(
                table: "ReportColumns",
                columns: new[] { "Id", "DefaultLabel", "DisplayOrder", "PropertyName", "ReportDefinitionId" },
                values: new object[,]
                {
                    { 1, "ID", 1, "Id", 1 },
                    { 2, "Name", 2, "Name", 1 },
                    { 3, "Age", 3, "Age", 1 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReportColumns_ReportDefinitionId",
                table: "ReportColumns",
                column: "ReportDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserColumnPreferences_UserId",
                table: "UserColumnPreferences",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReportColumns");

            migrationBuilder.DropTable(
                name: "UserColumnPreferences");

            migrationBuilder.DropTable(
                name: "ReportDefinitions");
        }
    }
}
