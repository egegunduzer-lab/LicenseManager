using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LicenseManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    KeyVersion = table.Column<int>(type: "integer", nullable: false),
                    PublicKey = table.Column<byte[]>(type: "bytea", nullable: false),
                    PrivateKey = table.Column<byte[]>(type: "bytea", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectKeys_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectKeys_ProjectId_KeyVersion",
                table: "ProjectKeys",
                columns: new[] { "ProjectId", "KeyVersion" },
                unique: true);


            migrationBuilder.Sql("""
    INSERT INTO "ProjectKeys"
    (
        "Id",
        "ProjectId",
        "KeyVersion",
        "PublicKey",
        "PrivateKey",
        "IsActive",
        "CreatedAt"
    )
    SELECT
        gen_random_uuid(),
        "Id",
        "KeyVersion",
        "PublicKey",
        "PrivateKey",
        TRUE,
        "CreatedAt"
    FROM "Projects";
    """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectKeys");
        }
    }
}
