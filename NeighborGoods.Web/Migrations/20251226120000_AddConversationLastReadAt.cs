using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeighborGoods.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationLastReadAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "Participant1LastReadAt",
                table: "Conversations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Participant2LastReadAt",
                table: "Conversations",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Participant1LastReadAt",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "Participant2LastReadAt",
                table: "Conversations");
        }
    }
}

