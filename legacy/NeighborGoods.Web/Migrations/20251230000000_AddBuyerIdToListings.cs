using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeighborGoods.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddBuyerIdToListings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BuyerId",
                table: "Listings",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Listings_BuyerId",
                table: "Listings",
                column: "BuyerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Listings_AspNetUsers_BuyerId",
                table: "Listings",
                column: "BuyerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Listings_AspNetUsers_BuyerId",
                table: "Listings");

            migrationBuilder.DropIndex(
                name: "IX_Listings_BuyerId",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "BuyerId",
                table: "Listings");
        }
    }
}

