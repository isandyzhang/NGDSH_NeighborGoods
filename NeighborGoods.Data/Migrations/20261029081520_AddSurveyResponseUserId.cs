using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeighborGoods.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSurveyResponseUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "SurveyResponses",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "");

            // 登入制上路前的匿名回覆無法對應帳號，清掉以免唯一索引衝突。
            migrationBuilder.Sql("DELETE FROM SurveyResponses WHERE UserId = N''");

            migrationBuilder.CreateIndex(
                name: "IX_SurveyResponses_UserId",
                table: "SurveyResponses",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SurveyResponses_UserId",
                table: "SurveyResponses");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "SurveyResponses");
        }
    }
}
