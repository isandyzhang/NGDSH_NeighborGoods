using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NeighborGoods.Data;

#nullable disable

namespace NeighborGoods.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(NeighborGoodsDbContext))]
    [Migration("20261108120000_AddListingPickupLocationText")]
    public class AddListingPickupLocationText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PickupLocationText",
                table: "Listings",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE l
                SET l.PickupLocationText = LEFT(COALESCE(NULLIF(p.DisplayName, N''), N'私訊'), 80)
                FROM Listings AS l
                LEFT JOIN ListingPickupLocations AS p ON p.Id = l.PickupLocation
                WHERE l.PickupLocationText = N''
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PickupLocationText",
                table: "Listings");
        }
    }
}
