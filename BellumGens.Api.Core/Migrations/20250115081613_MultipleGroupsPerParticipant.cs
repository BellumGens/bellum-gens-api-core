using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BellumGens.Api.Core.Migrations
{
    /// <inheritdoc />
    public partial class MultipleGroupsPerParticipant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TournamentApplications_TournamentCSGOGroups_TournamentCSGOGroupId",
                table: "TournamentApplications");

            migrationBuilder.DropForeignKey(
                name: "FK_TournamentApplications_TournamentSC2Groups_TournamentSC2GroupId",
                table: "TournamentApplications");

            migrationBuilder.DropForeignKey(
                name: "FK_TournamentCSGOMatches_TournamentCSGOGroups_GroupId",
                table: "TournamentCSGOMatches");

            migrationBuilder.DropForeignKey(
                name: "FK_TournamentSC2Groups_Tournaments_TournamentId",
                table: "TournamentSC2Groups");

            migrationBuilder.DropForeignKey(
                name: "FK_TournamentSC2Matches_TournamentSC2Groups_GroupId",
                table: "TournamentSC2Matches");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TournamentSC2Groups",
                table: "TournamentSC2Groups");

            migrationBuilder.DropIndex(
                name: "IX_TournamentApplications_TournamentCSGOGroupId",
                table: "TournamentApplications");

            migrationBuilder.DropIndex(
                name: "IX_TournamentApplications_TournamentSC2GroupId",
                table: "TournamentApplications");

            migrationBuilder.RenameTable(
                name: "TournamentSC2Groups",
                newName: "TournamentGroup");

            migrationBuilder.RenameIndex(
                name: "IX_TournamentSC2Groups_TournamentId",
                table: "TournamentGroup",
                newName: "IX_TournamentGroup_TournamentId");

            migrationBuilder.AddColumn<string>(
                name: "Discriminator",
                table: "TournamentGroup",
                type: "nvarchar(21)",
                maxLength: 21,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TournamentGroup",
                table: "TournamentGroup",
                column: "Id");

            migrationBuilder.Sql(@"UPDATE TournamentGroup 
                SET Discriminator = 'TournamentSC2Group'");

            migrationBuilder.Sql(@"INSERT INTO TournamentGroup (Id, Name, TournamentId, Discriminator)
                SELECT Id, Name, TournamentId, 'TournamentCSGOGroup'
                FROM TournamentCSGOGroups;");

            migrationBuilder.DropTable(
                name: "TournamentCSGOGroups");

            migrationBuilder.CreateTable(
                name: "TournamentGroupParticipants",
                columns: table => new
                {
                    TournamentGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TournamentApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentGroupParticipants", x => new { x.TournamentGroupId, x.TournamentApplicationId });
                    table.ForeignKey(
                        name: "FK_TournamentGroupParticipants_TournamentApplications_TournamentApplicationId",
                        column: x => x.TournamentApplicationId,
                        principalTable: "TournamentApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_TournamentGroupParticipants_TournamentGroup_TournamentGroupId",
                        column: x => x.TournamentGroupId,
                        principalTable: "TournamentGroup",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TournamentGroupParticipants_TournamentApplicationId",
                table: "TournamentGroupParticipants",
                column: "TournamentApplicationId");

            migrationBuilder.Sql(@"INSERT INTO TournamentGroupParticipants (TournamentGroupId, TournamentApplicationId, Points)
                SELECT TournamentCSGOGroupId, Id, '0'
                FROM TournamentApplications
                WHERE TournamentCSGOGroupId IS NOT NULL;");

            migrationBuilder.Sql(@"INSERT INTO TournamentGroupParticipants (TournamentGroupId, TournamentApplicationId, Points)
                SELECT TournamentSC2GroupId, Id, '0'
                FROM TournamentApplications
                WHERE TournamentSC2GroupId IS NOT NULL;");

            migrationBuilder.DropColumn(
                name: "TournamentCSGOGroupId",
                table: "TournamentApplications");

            migrationBuilder.DropColumn(
                name: "TournamentSC2GroupId",
                table: "TournamentApplications");

            migrationBuilder.AddForeignKey(
                name: "FK_TournamentCSGOMatches_TournamentGroup_GroupId",
                table: "TournamentCSGOMatches",
                column: "GroupId",
                principalTable: "TournamentGroup",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TournamentGroup_Tournaments_TournamentId",
                table: "TournamentGroup",
                column: "TournamentId",
                principalTable: "Tournaments",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TournamentSC2Matches_TournamentGroup_GroupId",
                table: "TournamentSC2Matches",
                column: "GroupId",
                principalTable: "TournamentGroup",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TournamentCSGOMatches_TournamentGroup_GroupId",
                table: "TournamentCSGOMatches");

            migrationBuilder.DropForeignKey(
                name: "FK_TournamentGroup_Tournaments_TournamentId",
                table: "TournamentGroup");

            migrationBuilder.DropForeignKey(
                name: "FK_TournamentSC2Matches_TournamentGroup_GroupId",
                table: "TournamentSC2Matches");

            migrationBuilder.DropTable(
                name: "TournamentGroupParticipants");

            migrationBuilder.CreateTable(
                name: "TournamentCSGOGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentCSGOGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TournamentCSGOGroups_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(@"INSERT INTO TournamentCSGOGroups (Id, Name, TournamentId)
                SELECT Id, Name, TournamentId
                FROM TournamentGroup
                WHERE Discriminator = 'TournamentCSGOGroup'");

            migrationBuilder.Sql(@"DELETE FROM TournamentGroup
                WHERE Discriminator = 'TournamentCSGOGroup'");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TournamentGroup",
                table: "TournamentGroup");

            migrationBuilder.DropColumn(
                name: "Discriminator",
                table: "TournamentGroup");

            migrationBuilder.RenameTable(
                name: "TournamentGroup",
                newName: "TournamentSC2Groups");

            migrationBuilder.RenameIndex(
                name: "IX_TournamentGroup_TournamentId",
                table: "TournamentSC2Groups",
                newName: "IX_TournamentSC2Groups_TournamentId");

            migrationBuilder.AddColumn<Guid>(
                name: "TournamentCSGOGroupId",
                table: "TournamentApplications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TournamentSC2GroupId",
                table: "TournamentApplications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_TournamentSC2Groups",
                table: "TournamentSC2Groups",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentApplications_TournamentCSGOGroupId",
                table: "TournamentApplications",
                column: "TournamentCSGOGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentApplications_TournamentSC2GroupId",
                table: "TournamentApplications",
                column: "TournamentSC2GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentCSGOGroups_TournamentId",
                table: "TournamentCSGOGroups",
                column: "TournamentId");

            migrationBuilder.AddForeignKey(
                name: "FK_TournamentApplications_TournamentCSGOGroups_TournamentCSGOGroupId",
                table: "TournamentApplications",
                column: "TournamentCSGOGroupId",
                principalTable: "TournamentCSGOGroups",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TournamentApplications_TournamentSC2Groups_TournamentSC2GroupId",
                table: "TournamentApplications",
                column: "TournamentSC2GroupId",
                principalTable: "TournamentSC2Groups",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TournamentCSGOMatches_TournamentCSGOGroups_GroupId",
                table: "TournamentCSGOMatches",
                column: "GroupId",
                principalTable: "TournamentCSGOGroups",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TournamentSC2Groups_Tournaments_TournamentId",
                table: "TournamentSC2Groups",
                column: "TournamentId",
                principalTable: "Tournaments",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TournamentSC2Matches_TournamentSC2Groups_GroupId",
                table: "TournamentSC2Matches",
                column: "GroupId",
                principalTable: "TournamentSC2Groups",
                principalColumn: "Id");
        }
    }
}
