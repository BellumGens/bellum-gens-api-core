using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BellumGens.Api.Core.Migrations
{
    /// <inheritdoc />
    public partial class Dota2Tournaments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TournamentDota2Matches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Team1Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Team2Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Team1Points = table.Column<int>(type: "int", nullable: false),
                    Team2Points = table.Column<int>(type: "int", nullable: false),
                    DemoLink = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VideoLink = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NoShow = table.Column<bool>(type: "bit", nullable: false),
                    StartTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentDota2Matches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TournamentDota2Matches_CSGOTeams_Team1Id",
                        column: x => x.Team1Id,
                        principalTable: "CSGOTeams",
                        principalColumn: "TeamId");
                    table.ForeignKey(
                        name: "FK_TournamentDota2Matches_CSGOTeams_Team2Id",
                        column: x => x.Team2Id,
                        principalTable: "CSGOTeams",
                        principalColumn: "TeamId");
                    table.ForeignKey(
                        name: "FK_TournamentDota2Matches_TournamentGroup_GroupId",
                        column: x => x.GroupId,
                        principalTable: "TournamentGroup",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TournamentDota2Matches_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "Dota2MatchGames",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Dota2MatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GameNumber = table.Column<int>(type: "int", nullable: false),
                    SteamMatchId = table.Column<long>(type: "bigint", nullable: true),
                    RadiantTeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WinnerTeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dota2MatchGames", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Dota2MatchGames_CSGOTeams_RadiantTeamId",
                        column: x => x.RadiantTeamId,
                        principalTable: "CSGOTeams",
                        principalColumn: "TeamId");
                    table.ForeignKey(
                        name: "FK_Dota2MatchGames_CSGOTeams_WinnerTeamId",
                        column: x => x.WinnerTeamId,
                        principalTable: "CSGOTeams",
                        principalColumn: "TeamId");
                    table.ForeignKey(
                        name: "FK_Dota2MatchGames_TournamentDota2Matches_Dota2MatchId",
                        column: x => x.Dota2MatchId,
                        principalTable: "TournamentDota2Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Dota2MatchGames_Dota2MatchId",
                table: "Dota2MatchGames",
                column: "Dota2MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Dota2MatchGames_RadiantTeamId",
                table: "Dota2MatchGames",
                column: "RadiantTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Dota2MatchGames_WinnerTeamId",
                table: "Dota2MatchGames",
                column: "WinnerTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentDota2Matches_GroupId",
                table: "TournamentDota2Matches",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentDota2Matches_Team1Id",
                table: "TournamentDota2Matches",
                column: "Team1Id");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentDota2Matches_Team2Id",
                table: "TournamentDota2Matches",
                column: "Team2Id");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentDota2Matches_TournamentId",
                table: "TournamentDota2Matches",
                column: "TournamentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Dota2MatchGames");

            migrationBuilder.DropTable(
                name: "TournamentDota2Matches");
        }
    }
}
