using Microsoft.EntityFrameworkCore.Migrations;

namespace BellumGens.Api.Core.Migrations
{
    public partial class BattleNetBattleTag : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BattleNetBattleTag",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BattleNetBattleTag",
                table: "AspNetUsers");
        }
    }
}
