using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LicenseManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class EnforceSingleActiveProjectKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ProjectKeys_ProjectId",
                table: "ProjectKeys",
                column: "ProjectId",
                unique: true,
                filter: "\"IsActive\" = TRUE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProjectKeys_ProjectId",
                table: "ProjectKeys");
        }
    }
}
