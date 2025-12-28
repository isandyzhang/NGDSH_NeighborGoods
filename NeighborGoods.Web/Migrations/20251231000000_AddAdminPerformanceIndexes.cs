using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeighborGoods.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Listings 索引
            migrationBuilder.CreateIndex(
                name: "IX_Listings_CreatedAt",
                table: "Listings",
                column: "CreatedAt");

            // Conversations 索引
            migrationBuilder.CreateIndex(
                name: "IX_Conversations_ListingId",
                table: "Conversations",
                column: "ListingId");

            // AdminMessages 索引
            migrationBuilder.CreateIndex(
                name: "IX_AdminMessages_IsRead",
                table: "AdminMessages",
                column: "IsRead");

            migrationBuilder.CreateIndex(
                name: "IX_AdminMessages_CreatedAt",
                table: "AdminMessages",
                column: "CreatedAt");

            // Users 索引
            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_CreatedAt",
                table: "AspNetUsers",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_CreatedAt",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AdminMessages_CreatedAt",
                table: "AdminMessages");

            migrationBuilder.DropIndex(
                name: "IX_AdminMessages_IsRead",
                table: "AdminMessages");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_ListingId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Listings_CreatedAt",
                table: "Listings");
        }
    }
}

