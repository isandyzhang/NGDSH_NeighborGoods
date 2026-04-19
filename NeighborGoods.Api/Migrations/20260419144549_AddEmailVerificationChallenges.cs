using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeighborGoods.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailVerificationChallenges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailVerificationChallenges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Purpose = table.Column<byte>(type: "tinyint", nullable: false),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CodeHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailVerificationChallenges", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerificationChallenges_Purpose_Email_Consumed_Expires",
                table: "EmailVerificationChallenges",
                columns: new[] { "Purpose", "NormalizedEmail", "ConsumedAt", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerificationChallenges_User_Purpose_Consumed",
                table: "EmailVerificationChallenges",
                columns: new[] { "UserId", "Purpose", "ConsumedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailVerificationChallenges");
        }
    }
}
