using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeighborGoods.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddQuickResponderBadgeFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsQuickResponder",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "QuickResponderEvaluatedAt",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QuickResponderP75Minutes",
                table: "AspNetUsers",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsQuickResponder",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "QuickResponderEvaluatedAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "QuickResponderP75Minutes",
                table: "AspNetUsers");
        }
    }
}
