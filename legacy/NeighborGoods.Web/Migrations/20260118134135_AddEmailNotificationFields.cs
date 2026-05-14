using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeighborGoods.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailNotificationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 只新增 Email 通知相關欄位
            migrationBuilder.AddColumn<bool>(
                name: "EmailNotificationEnabled",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailNotificationLastSentAt",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 只移除 Email 通知相關欄位
            migrationBuilder.DropColumn(
                name: "EmailNotificationEnabled",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "EmailNotificationLastSentAt",
                table: "AspNetUsers");
        }
    }
}
