using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeighborGoods.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddListingExpiryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AutoExpiredAt",
                table: "Listings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiryNoticeSentAt",
                table: "Listings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ListedAt",
                table: "Listings",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.Sql("UPDATE Listings SET ListedAt = CreatedAt WHERE ListedAt = '0001-01-01'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoExpiredAt",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "ExpiryNoticeSentAt",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "ListedAt",
                table: "Listings");
        }
    }
}
