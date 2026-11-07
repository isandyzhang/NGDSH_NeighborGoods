using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NeighborGoods.Data;

#nullable disable

namespace NeighborGoods.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(NeighborGoodsDbContext))]
    [Migration("20261107130000_AddLineUnreadNotificationLastSentAt")]
    public class AddLineUnreadNotificationLastSentAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LineUnreadNotificationLastSentAt",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LineUnreadNotificationLastSentAt",
                table: "AspNetUsers");
        }
    }
}
