using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BellumGens.Api.Core.Migrations
{
    /// <inheritdoc />
    public partial class TournamentApplicationChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "TournamentApplications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Discord",
                table: "TournamentApplications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "TournamentApplications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "TournamentApplications",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Country",
                table: "TournamentApplications");

            migrationBuilder.DropColumn(
                name: "Discord",
                table: "TournamentApplications");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "TournamentApplications");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "TournamentApplications");
        }
    }
}
