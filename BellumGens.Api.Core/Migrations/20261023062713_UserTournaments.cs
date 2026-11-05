using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BellumGens.Api.Core.Migrations
{
    /// <inheritdoc />
    public partial class UserTournaments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "StartDate",
                table: "Tournaments",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "EndDate",
                table: "Tournaments",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AddColumn<string>(
                name: "CreatorId",
                table: "Tournaments",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Game",
                table: "Tournaments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InviteCode",
                table: "Tournaments",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxParticipants",
                table: "Tournaments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrizePool",
                table: "Tournaments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Tournaments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Visibility",
                table: "Tournaments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Existing tournaments are public; the active one (the current league) is in progress and the others
            // get their status from their dates: ended -> completed, not started yet -> open, otherwise in progress.
            // Values: 1 = Open, 2 = InProgress, 3 = Completed (TournamentStatus).
            migrationBuilder.Sql(
                @"UPDATE [Tournaments] SET [Status] = CASE
                    WHEN [Active] = 1 THEN 2
                    WHEN [EndDate] < SYSDATETIMEOFFSET() THEN 3
                    WHEN [StartDate] > SYSDATETIMEOFFSET() THEN 1
                    ELSE 2
                END");

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_CreatorId",
                table: "Tournaments",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_InviteCode",
                table: "Tournaments",
                column: "InviteCode",
                unique: true,
                filter: "[InviteCode] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tournaments_CreatorId",
                table: "Tournaments");

            migrationBuilder.DropIndex(
                name: "IX_Tournaments_InviteCode",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "CreatorId",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "Game",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "InviteCode",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "MaxParticipants",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "PrizePool",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "Tournaments");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "StartDate",
                table: "Tournaments",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "EndDate",
                table: "Tournaments",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);
        }
    }
}
