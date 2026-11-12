using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NeighborGoods.Data;

#nullable disable

namespace NeighborGoods.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(NeighborGoodsDbContext))]
    [Migration("20261112120000_AddLineTransactionGroups")]
    public class AddLineTransactionGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LineGroupSharedAt",
                table: "Listings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LineTransactionGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LeftAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LineTransactionGroups", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LineTransactionGroups_GroupId",
                table: "LineTransactionGroups",
                column: "GroupId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LineTransactionGroups_IsActive",
                table: "LineTransactionGroups",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LineTransactionGroups");

            migrationBuilder.DropColumn(
                name: "LineGroupSharedAt",
                table: "Listings");
        }
    }
}
