using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataWarehousePower.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartmentSnowflakeConnection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TBL_DepartmentSnowflakeConnections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    SnowflakeConnectionStringId = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TBL_DepartmentSnowflakeConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TBL_DepartmentSnowflakeConnections_TBL_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "TBL_Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TBL_DepartmentSnowflakeConnections_TBL_SnowflakeConnectionStrings_SnowflakeConnectionStringId",
                        column: x => x.SnowflakeConnectionStringId,
                        principalTable: "TBL_SnowflakeConnectionStrings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TBL_DepartmentSnowflakeConnections_DepartmentId",
                table: "TBL_DepartmentSnowflakeConnections",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_TBL_DepartmentSnowflakeConnections_SnowflakeConnectionStringId",
                table: "TBL_DepartmentSnowflakeConnections",
                column: "SnowflakeConnectionStringId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TBL_DepartmentSnowflakeConnections");
        }
    }
}
