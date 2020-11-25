using Microsoft.EntityFrameworkCore.Migrations;

namespace BellumGens.Api.Core.Migrations
{
    public partial class TwitchId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TwitchId",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TwitchId",
                table: "AspNetUsers");
        }
    }
}
