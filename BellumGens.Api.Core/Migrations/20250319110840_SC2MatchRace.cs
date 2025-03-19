using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BellumGens.Api.Core.Migrations
{
    /// <inheritdoc />
    public partial class SC2MatchRace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Player1Race",
                table: "TournamentSC2Matches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Player2Race",
                table: "TournamentSC2Matches",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Player1Race",
                table: "TournamentSC2Matches");

            migrationBuilder.DropColumn(
                name: "Player2Race",
                table: "TournamentSC2Matches");
        }
    }
}
