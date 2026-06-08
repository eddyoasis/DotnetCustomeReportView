using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DataWarehousePower.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReportStaff",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Age = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportStaff", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "ReportStaff",
                columns: new[] { "Id", "Age", "Name" },
                values: new object[,]
                {
                    { 1, 30, "Alice Johnson" },
                    { 2, 45, "Bob Smith" },
                    { 3, 28, "Carol Williams" },
                    { 4, 52, "David Brown" },
                    { 5, 35, "Eve Davis" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReportStaff");
        }
    }
}
