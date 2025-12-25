using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeighborGoods.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddListingCondition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Condition",
                table: "Listings",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Condition",
                table: "Listings");
        }
    }
}
