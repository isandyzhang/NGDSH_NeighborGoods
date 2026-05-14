using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeighborGoods.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationListingId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 先刪除所有現有的對話（因為所有對話都必須關聯商品）
            migrationBuilder.Sql("DELETE FROM [Conversations]");

            // 添加 ListingId 欄位（不可為空）
            migrationBuilder.AddColumn<Guid>(
                name: "ListingId",
                table: "Conversations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: Guid.Empty);

            // 建立外鍵關聯
            migrationBuilder.CreateIndex(
                name: "IX_Conversations_ListingId",
                table: "Conversations",
                column: "ListingId");

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_Listings_ListingId",
                table: "Conversations",
                column: "ListingId",
                principalTable: "Listings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // 更新索引以包含 ListingId
            migrationBuilder.DropIndex(
                name: "IX_Conversations_Participant1Id_Participant2Id",
                table: "Conversations");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_Participant1Id_Participant2Id_ListingId",
                table: "Conversations",
                columns: new[] { "Participant1Id", "Participant2Id", "ListingId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 移除外鍵和索引
            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_Listings_ListingId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_ListingId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_Participant1Id_Participant2Id_ListingId",
                table: "Conversations");

            // 移除欄位
            migrationBuilder.DropColumn(
                name: "ListingId",
                table: "Conversations");

            // 恢復原來的索引
            migrationBuilder.CreateIndex(
                name: "IX_Conversations_Participant1Id_Participant2Id",
                table: "Conversations",
                columns: new[] { "Participant1Id", "Participant2Id" });
        }
    }
}

