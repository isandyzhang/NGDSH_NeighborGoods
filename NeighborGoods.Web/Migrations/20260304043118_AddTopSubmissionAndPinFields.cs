using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeighborGoods.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddTopSubmissionAndPinFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPinned",
                table: "Listings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PinnedEndDate",
                table: "Listings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PinnedStartDate",
                table: "Listings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TopPinCredits",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ListingTopSubmissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ListingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PhotoBlobName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FeedbackTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FeedbackDetail = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    AllowPromotion = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedByAdminId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    GrantedCredits = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingTopSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ListingTopSubmissions_AspNetUsers_ReviewedByAdminId",
                        column: x => x.ReviewedByAdminId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ListingTopSubmissions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ListingTopSubmissions_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ListingTopSubmissions_CreatedAt",
                table: "ListingTopSubmissions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ListingTopSubmissions_ListingId",
                table: "ListingTopSubmissions",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_ListingTopSubmissions_ReviewedByAdminId",
                table: "ListingTopSubmissions",
                column: "ReviewedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_ListingTopSubmissions_Status",
                table: "ListingTopSubmissions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ListingTopSubmissions_UserId",
                table: "ListingTopSubmissions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ListingTopSubmissions");

            migrationBuilder.DropColumn(
                name: "IsPinned",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "PinnedEndDate",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "PinnedStartDate",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "TopPinCredits",
                table: "AspNetUsers");
        }
    }
}
