using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeighborGoods.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSurveyResponses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SurveyResponses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResidentType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Building = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    Floor = table.Column<int>(type: "int", nullable: true),
                    RatingUi = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    RatingListing = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    RatingInteraction = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    RatingGuide = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    RatingPromo = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Feedback = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ConsentAccepted = table.Column<bool>(type: "bit", nullable: false),
                    ActivityName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsGiftEligible = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurveyResponses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SurveyResponses_CreatedAt",
                table: "SurveyResponses",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SurveyResponses_IsGiftEligible",
                table: "SurveyResponses",
                column: "IsGiftEligible");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SurveyResponses");
        }
    }
}
