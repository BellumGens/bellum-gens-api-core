using Microsoft.EntityFrameworkCore.Migrations;

namespace BellumGens.Api.Core.Migrations
{
    public partial class MapPoolTableRename : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_dbo.UserMapPools_dbo.AspNetUsers_UserId",
                table: "UserMapPools");

            migrationBuilder.DropPrimaryKey(
                name: "PK_dbo.UserMapPools",
                table: "UserMapPools");

            migrationBuilder.RenameTable(
                name: "UserMapPools",
                newName: "UserMapPool");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserMapPool",
                table: "UserMapPool",
                columns: new[] { "UserId", "Map" });

            migrationBuilder.AddForeignKey(
                name: "FK_UserMapPool_AspNetUsers_UserId",
                table: "UserMapPool",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserMapPool_AspNetUsers_UserId",
                table: "UserMapPool");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserMapPool",
                table: "UserMapPool");

            migrationBuilder.RenameTable(
                name: "UserMapPool",
                newName: "UserMapPools");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserMapPools",
                table: "UserMapPools",
                columns: new[] { "UserId", "Map" });

            migrationBuilder.AddForeignKey(
                name: "FK_UserMapPools_AspNetUsers_UserId",
                table: "UserMapPools",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
