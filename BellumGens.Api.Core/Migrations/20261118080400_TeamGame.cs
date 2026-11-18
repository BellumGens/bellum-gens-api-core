using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BellumGens.Api.Core.Migrations
{
    /// <inheritdoc />
    public partial class TeamGame : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Game",
                table: "CSGOTeams",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Game",
                table: "CSGOTeams");
        }
    }
}
