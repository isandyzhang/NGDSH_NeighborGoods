using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeighborGoods.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 為 Messages.CreatedAt 建立索引（用於排序和未讀計算）
            migrationBuilder.CreateIndex(
                name: "IX_Messages_CreatedAt",
                table: "Messages",
                column: "CreatedAt");

            // 為 Listings.Status 和 SellerId 建立複合索引（用於查詢用戶的商品）
            migrationBuilder.CreateIndex(
                name: "IX_Listings_Status_SellerId",
                table: "Listings",
                columns: new[] { "Status", "SellerId" });

            // 為 Conversations.UpdatedAt 建立索引（用於對話列表排序）
            migrationBuilder.CreateIndex(
                name: "IX_Conversations_UpdatedAt",
                table: "Conversations",
                column: "UpdatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Messages_CreatedAt",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Listings_Status_SellerId",
                table: "Listings");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_UpdatedAt",
                table: "Conversations");
        }
    }
}

