using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeighborGoods.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddListingCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "Listings",
                type: "int",
                nullable: false,
                defaultValue: 9);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                table: "Listings");
        }
    }
}
