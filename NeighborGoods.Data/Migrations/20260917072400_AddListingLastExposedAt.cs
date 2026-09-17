using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeighborGoods.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddListingLastExposedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastExposedAt",
                table: "Listings",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastExposedAt",
                table: "Listings");
        }
    }
}
