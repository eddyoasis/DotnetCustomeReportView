using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataWarehousePower.Migrations
{
    /// <inheritdoc />
    public partial class AddDataFileFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TBL_DataFileDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DataFileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SourceDatabase = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SourceTable = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SourceSP = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Parameters = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Departments = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TBL_DataFileDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TBL_DataFileColumns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DataFileDefinitionId = table.Column<int>(type: "int", nullable: false),
                    PropertyName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DefaultLabel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MappingParameter = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TBL_DataFileColumns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TBL_DataFileColumns_TBL_DataFileDefinitions_DataFileDefinitionId",
                        column: x => x.DataFileDefinitionId,
                        principalTable: "TBL_DataFileDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TBL_DataFileColumns_DataFileDefinitionId",
                table: "TBL_DataFileColumns",
                column: "DataFileDefinitionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TBL_DataFileColumns");

            migrationBuilder.DropTable(
                name: "TBL_DataFileDefinitions");
        }
    }
}
