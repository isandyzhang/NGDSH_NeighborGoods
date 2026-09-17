using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeighborGoods.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHousingLookupFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "ListingResidences",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "District",
                table: "ListingResidences",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "ListingResidences",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResidenceId",
                table: "ListingPickupLocations",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ListingPickupLocations_ResidenceId",
                table: "ListingPickupLocations",
                column: "ResidenceId");

            migrationBuilder.AddForeignKey(
                name: "FK_ListingPickupLocations_ListingResidences_ResidenceId",
                table: "ListingPickupLocations",
                column: "ResidenceId",
                principalTable: "ListingResidences",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.InsertData(
                table: "ListingPickupLocations",
                columns: ["Id", "CodeKey", "DisplayName", "SortOrder", "IsActive", "ResidenceId"],
                values: new object[,]
                {
                    { 4, "pickup-4", "北棟管理室", 0, true, 1 },
                    { 5, "pickup-5", "南棟管理室", 1, true, 1 },
                    { 6, "pickup-6", "風雨操場", 2, true, 1 },
                    { 7, "pickup-7", "北棟管理室", 0, true, 2 },
                    { 8, "pickup-8", "南棟管理室", 1, true, 2 },
                    { 9, "pickup-9", "風雨操場", 2, true, 2 },
                    { 10, "pickup-10", "北棟管理室", 0, true, 3 },
                    { 11, "pickup-11", "南棟管理室", 1, true, 3 },
                    { 12, "pickup-12", "風雨操場", 2, true, 3 },
                });

            migrationBuilder.Sql(
                """
                UPDATE [Listings] SET [PickupLocation] = 4 WHERE [Residence] = 1 AND [PickupLocation] = 0;
                UPDATE [Listings] SET [PickupLocation] = 5 WHERE [Residence] = 1 AND [PickupLocation] = 1;
                UPDATE [Listings] SET [PickupLocation] = 6 WHERE [Residence] = 1 AND [PickupLocation] = 2;
                UPDATE [Listings] SET [PickupLocation] = 7 WHERE [Residence] = 2 AND [PickupLocation] = 0;
                UPDATE [Listings] SET [PickupLocation] = 8 WHERE [Residence] = 2 AND [PickupLocation] = 1;
                UPDATE [Listings] SET [PickupLocation] = 9 WHERE [Residence] = 2 AND [PickupLocation] = 2;
                UPDATE [Listings] SET [PickupLocation] = 10 WHERE [Residence] = 3 AND [PickupLocation] = 0;
                UPDATE [Listings] SET [PickupLocation] = 11 WHERE [Residence] = 3 AND [PickupLocation] = 1;
                UPDATE [Listings] SET [PickupLocation] = 12 WHERE [Residence] = 3 AND [PickupLocation] = 2;
                UPDATE [ListingPickupLocations] SET [IsActive] = 0 WHERE [Id] IN (0, 1, 2);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE [Listings] SET [PickupLocation] = 0 WHERE [PickupLocation] IN (4, 7, 10);
                UPDATE [Listings] SET [PickupLocation] = 1 WHERE [PickupLocation] IN (5, 8, 11);
                UPDATE [Listings] SET [PickupLocation] = 2 WHERE [PickupLocation] IN (6, 9, 12);
                DELETE FROM [ListingPickupLocations] WHERE [Id] BETWEEN 4 AND 12;
                UPDATE [ListingPickupLocations] SET [IsActive] = 1 WHERE [Id] IN (0, 1, 2);
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_ListingPickupLocations_ListingResidences_ResidenceId",
                table: "ListingPickupLocations");

            migrationBuilder.DropIndex(
                name: "IX_ListingPickupLocations_ResidenceId",
                table: "ListingPickupLocations");

            migrationBuilder.DropColumn(
                name: "ResidenceId",
                table: "ListingPickupLocations");

            migrationBuilder.DropColumn(
                name: "City",
                table: "ListingResidences");

            migrationBuilder.DropColumn(
                name: "District",
                table: "ListingResidences");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "ListingResidences");
        }
    }
}
