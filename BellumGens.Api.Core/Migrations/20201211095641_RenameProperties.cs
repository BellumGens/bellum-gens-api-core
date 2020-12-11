using Microsoft.EntityFrameworkCore.Migrations;

namespace BellumGens.Api.Core.Migrations
{
    public partial class RenameProperties : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            //migrationBuilder.DropForeignKey(
            //    name: "FK_TournamentCSGOMatchMaps_CSGOTeams_TeamBanId",
            //    table: "TournamentCSGOMatchMaps");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_TournamentCSGOMatchMaps_CSGOTeams_TeamPickId",
            //    table: "TournamentCSGOMatchMaps");

            migrationBuilder.DropForeignKey(
                name: "FK_dbo.CSGOMatchMaps_dbo.TournamentCSGOMatches_CSGOMatchId",
                table: "CSGOMatchMaps");

            migrationBuilder.DropForeignKey(
                name: "FK_dbo.SC2MatchMap_dbo.AspNetUsers_PlayerBanId",
                table: "SC2MatchMap");

            migrationBuilder.DropForeignKey(
                name: "FK_dbo.SC2MatchMap_dbo.AspNetUsers_PlayerPickId",
                table: "SC2MatchMap");

            migrationBuilder.DropForeignKey(
                name: "FK_dbo.SC2MatchMap_dbo.TournamentSC2Match_SC2MatchId",
                table: "SC2MatchMap");

            migrationBuilder.DropPrimaryKey(
                name: "PK_dbo.SC2MatchMap",
                table: "SC2MatchMap");

            //migrationBuilder.DropPrimaryKey(
            //    name: "PK_TournamentCSGOMatchMaps",
            //    table: "TournamentCSGOMatchMaps");

            migrationBuilder.RenameTable(
                name: "TournamentSC2Match",
                newName: "TournamentSC2Matches");

            migrationBuilder.RenameTable(
                name: "TournamentSC2Group",
                newName: "TournamentSC2Groups");

            migrationBuilder.RenameTable(
                name: "SC2MatchMap",
                newName: "SC2MatchMaps"); 
            
            migrationBuilder.RenameColumn(
                 table: "SC2MatchMaps",
                 name: "SC2MatchId",
                 newName: "Sc2MatchId");

            migrationBuilder.RenameColumn(
                table: "CSGOMatchMaps",
                name: "CSGOMatchId",
                newName: "CsgoMatchId");

            //migrationBuilder.RenameTable(
            //    name: "TournamentCSGOMatchMaps",
            //    newName: "CSGOMatchMaps");

            migrationBuilder.RenameIndex(
                name: "IX_SC2MatchId",
                table: "SC2MatchMaps",
                newName: "IX_SC2MatchMaps_Sc2MatchId");

            migrationBuilder.RenameIndex(
                name: "IX_PlayerPickId",
                table: "SC2MatchMaps",
                newName: "IX_SC2MatchMaps_PlayerPickId");

            migrationBuilder.RenameIndex(
                name: "IX_PlayerBanId",
                table: "SC2MatchMaps",
                newName: "IX_SC2MatchMaps_PlayerBanId");

            //migrationBuilder.RenameIndex(
            //    name: "IX_TournamentCSGOMatchMaps_TeamPickId",
            //    table: "CSGOMatchMaps",
            //    newName: "IX_CSGOMatchMaps_TeamPickId");

            //migrationBuilder.RenameIndex(
            //    name: "IX_TournamentCSGOMatchMaps_TeamBanId",
            //    table: "CSGOMatchMaps",
            //    newName: "IX_CSGOMatchMaps_TeamBanId");

            migrationBuilder.RenameIndex(
                name: "IX_CSGOMatchId",
                table: "CSGOMatchMaps",
                newName: "IX_CSGOMatchMaps_CsgoMatchId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SC2MatchMaps",
                table: "SC2MatchMaps",
                column: "Id");

            //migrationBuilder.AddPrimaryKey(
            //    name: "PK_CSGOMatchMaps",
            //    table: "CSGOMatchMaps",
            //    column: "Id");

            //migrationBuilder.AddForeignKey(
            //    name: "FK_CSGOMatchMaps_CSGOTeams_TeamBanId",
            //    table: "CSGOMatchMaps",
            //    column: "TeamBanId",
            //    principalTable: "CSGOTeams",
            //    principalColumn: "TeamId",
            //    onDelete: ReferentialAction.Restrict);

            //migrationBuilder.AddForeignKey(
            //    name: "FK_CSGOMatchMaps_CSGOTeams_TeamPickId",
            //    table: "CSGOMatchMaps",
            //    column: "TeamPickId",
            //    principalTable: "CSGOTeams",
            //    principalColumn: "TeamId",
            //    onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CSGOMatchMaps_TournamentCSGOMatches_CsgoMatchId",
                table: "CSGOMatchMaps",
                column: "CsgoMatchId",
                principalTable: "TournamentCSGOMatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SC2MatchMaps_AspNetUsers_PlayerBanId",
                table: "SC2MatchMaps",
                column: "PlayerBanId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SC2MatchMaps_AspNetUsers_PlayerPickId",
                table: "SC2MatchMaps",
                column: "PlayerPickId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SC2MatchMaps_TournamentSC2Matches_Sc2MatchId",
                table: "SC2MatchMaps",
                column: "Sc2MatchId",
                principalTable: "TournamentSC2Matches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            //migrationBuilder.DropForeignKey(
            //    name: "FK_CSGOMatchMaps_CSGOTeams_TeamBanId",
            //    table: "CSGOMatchMaps");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_CSGOMatchMaps_CSGOTeams_TeamPickId",
            //    table: "CSGOMatchMaps");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_CSGOMatchMaps_TournamentCSGOMatches_CsgoMatchId",
            //    table: "CSGOMatchMaps");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_SC2MatchMaps_AspNetUsers_PlayerBanId",
            //    table: "SC2MatchMaps");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_SC2MatchMaps_AspNetUsers_PlayerPickId",
            //    table: "SC2MatchMaps");

            //migrationBuilder.DropForeignKey(
            //    name: "FK_SC2MatchMaps_TournamentSC2Matches_Sc2MatchId",
            //    table: "SC2MatchMaps");

            //migrationBuilder.DropPrimaryKey(
            //    name: "PK_SC2MatchMaps",
            //    table: "SC2MatchMaps");

            //migrationBuilder.DropPrimaryKey(
            //    name: "PK_CSGOMatchMaps",
            //    table: "CSGOMatchMaps");

            //migrationBuilder.RenameTable(
            //    name: "SC2MatchMaps",
            //    newName: "TournamentSC2MatchMaps");

            //migrationBuilder.RenameTable(
            //    name: "CSGOMatchMaps",
            //    newName: "TournamentCSGOMatchMaps");

            //migrationBuilder.RenameIndex(
            //    name: "IX_SC2MatchMaps_Sc2MatchId",
            //    table: "TournamentSC2MatchMaps",
            //    newName: "IX_TournamentSC2MatchMaps_Sc2MatchId");

            //migrationBuilder.RenameIndex(
            //    name: "IX_SC2MatchMaps_PlayerPickId",
            //    table: "TournamentSC2MatchMaps",
            //    newName: "IX_TournamentSC2MatchMaps_PlayerPickId");

            //migrationBuilder.RenameIndex(
            //    name: "IX_SC2MatchMaps_PlayerBanId",
            //    table: "TournamentSC2MatchMaps",
            //    newName: "IX_TournamentSC2MatchMaps_PlayerBanId");

            //migrationBuilder.RenameIndex(
            //    name: "IX_CSGOMatchMaps_TeamPickId",
            //    table: "TournamentCSGOMatchMaps",
            //    newName: "IX_TournamentCSGOMatchMaps_TeamPickId");

            //migrationBuilder.RenameIndex(
            //    name: "IX_CSGOMatchMaps_TeamBanId",
            //    table: "TournamentCSGOMatchMaps",
            //    newName: "IX_TournamentCSGOMatchMaps_TeamBanId");

            //migrationBuilder.RenameIndex(
            //    name: "IX_CSGOMatchMaps_CsgoMatchId",
            //    table: "TournamentCSGOMatchMaps",
            //    newName: "IX_TournamentCSGOMatchMaps_CsgoMatchId");

            //migrationBuilder.AddPrimaryKey(
            //    name: "PK_TournamentSC2MatchMaps",
            //    table: "TournamentSC2MatchMaps",
            //    column: "Id");

            //migrationBuilder.AddPrimaryKey(
            //    name: "PK_TournamentCSGOMatchMaps",
            //    table: "TournamentCSGOMatchMaps",
            //    column: "Id");

            //migrationBuilder.AddForeignKey(
            //    name: "FK_TournamentCSGOMatchMaps_CSGOTeams_TeamBanId",
            //    table: "TournamentCSGOMatchMaps",
            //    column: "TeamBanId",
            //    principalTable: "CSGOTeams",
            //    principalColumn: "TeamId",
            //    onDelete: ReferentialAction.Restrict);

            //migrationBuilder.AddForeignKey(
            //    name: "FK_TournamentCSGOMatchMaps_CSGOTeams_TeamPickId",
            //    table: "TournamentCSGOMatchMaps",
            //    column: "TeamPickId",
            //    principalTable: "CSGOTeams",
            //    principalColumn: "TeamId",
            //    onDelete: ReferentialAction.Restrict);

            //migrationBuilder.AddForeignKey(
            //    name: "FK_TournamentCSGOMatchMaps_TournamentCSGOMatches_CsgoMatchId",
            //    table: "TournamentCSGOMatchMaps",
            //    column: "CsgoMatchId",
            //    principalTable: "TournamentCSGOMatches",
            //    principalColumn: "Id",
            //    onDelete: ReferentialAction.Cascade);

            //migrationBuilder.AddForeignKey(
            //    name: "FK_TournamentSC2MatchMaps_AspNetUsers_PlayerBanId",
            //    table: "TournamentSC2MatchMaps",
            //    column: "PlayerBanId",
            //    principalTable: "AspNetUsers",
            //    principalColumn: "Id",
            //    onDelete: ReferentialAction.Restrict);

            //migrationBuilder.AddForeignKey(
            //    name: "FK_TournamentSC2MatchMaps_AspNetUsers_PlayerPickId",
            //    table: "TournamentSC2MatchMaps",
            //    column: "PlayerPickId",
            //    principalTable: "AspNetUsers",
            //    principalColumn: "Id",
            //    onDelete: ReferentialAction.Restrict);

            //migrationBuilder.AddForeignKey(
            //    name: "FK_TournamentSC2MatchMaps_TournamentSC2Matches_Sc2MatchId",
            //    table: "TournamentSC2MatchMaps",
            //    column: "Sc2MatchId",
            //    principalTable: "TournamentSC2Matches",
            //    principalColumn: "Id",
            //    onDelete: ReferentialAction.Cascade);
        }
    }
}
