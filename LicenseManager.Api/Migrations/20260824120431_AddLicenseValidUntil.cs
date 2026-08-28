using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LicenseManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLicenseValidUntil : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // -------------------------------------------------
            // 1. ValidUntil kolonunu önce NULL kabul edecek
            //    şekilde ekliyoruz.
            //
            // Bunun sebebi mevcut License kayıtlarının
            // henüz ValidUntil değerine sahip olmaması.
            // -------------------------------------------------
            migrationBuilder.AddColumn<DateTime>(
                name: "ValidUntil",
                table: "Licenses",
                type: "timestamp with time zone",
                nullable: true);


            // -------------------------------------------------
            // 2. Mevcut lisansları taşıyoruz.
            //
            // Eski sistemde gerçek lisans süresi ile
            // signed-file expiry ayrılmadığı için,
            // en güvenli başlangıç değeri mevcut ExpiryDate.
            //
            // Yani migration mevcut lisansların süresini
            // kendiliğinden uzatmıyor.
            // -------------------------------------------------
            migrationBuilder.Sql(
                """
                UPDATE "Licenses"
                SET "ValidUntil" = "ExpiryDate"
                WHERE "ValidUntil" IS NULL;
                """);


            // -------------------------------------------------
            // 3. Artık bütün mevcut satırlarda değer var.
            //    Kolonu NOT NULL yapabiliriz.
            // -------------------------------------------------
            migrationBuilder.AlterColumn<DateTime>(
                name: "ValidUntil",
                table: "Licenses",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }


        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ValidUntil",
                table: "Licenses");
        }
    }
}