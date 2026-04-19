using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeighborGoods.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddListingConditionResidencePickupLookups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ListingConditions",
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
                    table.PrimaryKey("PK_ListingConditions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ListingPickupLocations",
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
                    table.PrimaryKey("PK_ListingPickupLocations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ListingResidences",
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
                    table.PrimaryKey("PK_ListingResidences", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "ListingConditions",
                columns: ["Id", "CodeKey", "DisplayName", "SortOrder", "IsActive"],
                values: new object[,]
                {
                    { 0, "New", "全新", 0, true },
                    { 1, "LikeNew", "近全新", 1, true },
                    { 2, "Good", "良好", 2, true },
                    { 3, "Fair", "普通", 3, true },
                    { 4, "WellUsed", "歲月痕跡", 4, true }
                });

            migrationBuilder.InsertData(
                table: "ListingResidences",
                columns: ["Id", "CodeKey", "DisplayName", "SortOrder", "IsActive"],
                values: new object[,]
                {
                    { 0, "Unknown", "未指定", 0, true },
                    { 1, "Factory", "機廠", 1, true },
                    { 2, "DongMing", "東明", 2, true },
                    { 3, "XiaoWan", "小彎", 3, true }
                });

            migrationBuilder.InsertData(
                table: "ListingPickupLocations",
                columns: ["Id", "CodeKey", "DisplayName", "SortOrder", "IsActive"],
                values: new object[,]
                {
                    { 0, "NorthBuilding", "北棟管理室", 0, true },
                    { 1, "SouthBuilding", "南棟管理室", 1, true },
                    { 2, "Gym", "風雨操場", 2, true },
                    { 3, "Message", "私訊", 3, true }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Listings_Condition",
                table: "Listings",
                column: "Condition");

            migrationBuilder.CreateIndex(
                name: "IX_Listings_PickupLocation",
                table: "Listings",
                column: "PickupLocation");

            migrationBuilder.CreateIndex(
                name: "IX_Listings_Residence",
                table: "Listings",
                column: "Residence");

            migrationBuilder.CreateIndex(
                name: "IX_ListingConditions_CodeKey",
                table: "ListingConditions",
                column: "CodeKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ListingPickupLocations_CodeKey",
                table: "ListingPickupLocations",
                column: "CodeKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ListingResidences_CodeKey",
                table: "ListingResidences",
                column: "CodeKey",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Listings_ListingConditions_Condition",
                table: "Listings",
                column: "Condition",
                principalTable: "ListingConditions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Listings_ListingPickupLocations_PickupLocation",
                table: "Listings",
                column: "PickupLocation",
                principalTable: "ListingPickupLocations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Listings_ListingResidences_Residence",
                table: "Listings",
                column: "Residence",
                principalTable: "ListingResidences",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Listings_ListingConditions_Condition",
                table: "Listings");

            migrationBuilder.DropForeignKey(
                name: "FK_Listings_ListingPickupLocations_PickupLocation",
                table: "Listings");

            migrationBuilder.DropForeignKey(
                name: "FK_Listings_ListingResidences_Residence",
                table: "Listings");

            migrationBuilder.DropTable(
                name: "ListingConditions");

            migrationBuilder.DropTable(
                name: "ListingPickupLocations");

            migrationBuilder.DropTable(
                name: "ListingResidences");

            migrationBuilder.DropIndex(
                name: "IX_Listings_Condition",
                table: "Listings");

            migrationBuilder.DropIndex(
                name: "IX_Listings_PickupLocation",
                table: "Listings");

            migrationBuilder.DropIndex(
                name: "IX_Listings_Residence",
                table: "Listings");
        }
    }
}
