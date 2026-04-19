using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeighborGoods.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddListingCategoriesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ListingCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    CodeKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingCategories", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "ListingCategories",
                columns: ["Id", "CodeKey", "DisplayName", "SortOrder", "IsActive"],
                values: new object[,]
                {
                    { 0, "Furniture", "家具家飾", 0, true },
                    { 1, "Electronics", "電子產品", 1, true },
                    { 2, "Clothing", "服飾配件", 2, true },
                    { 3, "Books", "書籍文具", 3, true },
                    { 4, "Sports", "運動用品", 4, true },
                    { 5, "Toys", "玩具遊戲", 5, true },
                    { 6, "Kitchen", "廚房用品", 6, true },
                    { 7, "Daily", "生活用品", 7, true },
                    { 8, "Baby", "嬰幼兒用品", 8, true },
                    { 9, "Other", "其他", 9, true }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Listings_Category",
                table: "Listings",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_ListingCategories_CodeKey",
                table: "ListingCategories",
                column: "CodeKey",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Listings_ListingCategories_Category",
                table: "Listings",
                column: "Category",
                principalTable: "ListingCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Listings_ListingCategories_Category",
                table: "Listings");

            migrationBuilder.DropTable(
                name: "ListingCategories");

            migrationBuilder.DropIndex(
                name: "IX_Listings_Category",
                table: "Listings");
        }
    }
}
