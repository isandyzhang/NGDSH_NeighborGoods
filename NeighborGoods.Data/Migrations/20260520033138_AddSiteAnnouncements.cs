using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeighborGoods.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteAnnouncements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SiteAnnouncements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Severity = table.Column<byte>(type: "tinyint", nullable: false),
                    Scope = table.Column<byte>(type: "tinyint", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    StartsAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndsAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LinkUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    LinkLabel = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteAnnouncements", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SiteAnnouncements_Active_Display",
                table: "SiteAnnouncements",
                columns: new[] { "IsEnabled", "StartsAt", "EndsAt", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SiteAnnouncements");
        }
    }
}
