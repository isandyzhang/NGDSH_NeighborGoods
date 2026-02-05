using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NeighborGoods.Web.Data;

#nullable disable

namespace NeighborGoods.Web.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260204100000_AddListingIsTradeable")]
    public partial class AddListingIsTradeable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTradeable",
                table: "Listings",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsTradeable",
                table: "Listings");
        }
    }
}

