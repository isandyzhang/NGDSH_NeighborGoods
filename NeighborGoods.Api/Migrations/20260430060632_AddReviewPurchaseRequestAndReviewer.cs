using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeighborGoods.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewPurchaseRequestAndReviewer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reviews_ListingId_BuyerId",
                table: "Reviews");

            migrationBuilder.AddColumn<Guid>(
                name: "PurchaseRequestId",
                table: "Reviews",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewerId",
                table: "Reviews",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE r
                SET r.ReviewerId = r.BuyerId
                FROM Reviews r
                WHERE r.ReviewerId IS NULL;

                UPDATE r
                SET r.PurchaseRequestId = x.Id
                FROM Reviews r
                CROSS APPLY (
                    SELECT TOP 1 pr_inner.Id AS Id
                    FROM PurchaseRequests pr_inner
                    WHERE pr_inner.ListingId = r.ListingId
                      AND pr_inner.BuyerId = r.BuyerId
                      AND pr_inner.[Status] = 1
                    ORDER BY pr_inner.RespondedAt DESC, pr_inner.CreatedAt DESC
                ) AS x
                WHERE r.PurchaseRequestId IS NULL;

                UPDATE r
                SET r.PurchaseRequestId = y.Id
                FROM Reviews r
                CROSS APPLY (
                    SELECT TOP 1 pr_inner.Id AS Id
                    FROM PurchaseRequests pr_inner
                    WHERE pr_inner.ListingId = r.ListingId
                      AND pr_inner.BuyerId = r.BuyerId
                    ORDER BY pr_inner.CreatedAt DESC
                ) AS y
                WHERE r.PurchaseRequestId IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ListingId_BuyerId",
                table: "Reviews",
                columns: new[] { "ListingId", "BuyerId" });

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_PurchaseRequestId_ReviewerId",
                table: "Reviews",
                columns: new[] { "PurchaseRequestId", "ReviewerId" },
                unique: true,
                filter: "[PurchaseRequestId] IS NOT NULL AND [ReviewerId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Reviews_PurchaseRequests_PurchaseRequestId",
                table: "Reviews",
                column: "PurchaseRequestId",
                principalTable: "PurchaseRequests",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reviews_PurchaseRequests_PurchaseRequestId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_ListingId_BuyerId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_PurchaseRequestId_ReviewerId",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "PurchaseRequestId",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "ReviewerId",
                table: "Reviews");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ListingId_BuyerId",
                table: "Reviews",
                columns: new[] { "ListingId", "BuyerId" },
                unique: true);
        }
    }
}
