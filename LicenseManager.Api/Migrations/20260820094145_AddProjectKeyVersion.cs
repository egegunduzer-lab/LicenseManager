using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LicenseManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectKeyVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "KeyVersion",
                table: "Projects",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KeyVersion",
                table: "Projects");
        }
    }
}
