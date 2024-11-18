using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BellumGens.Api.Core.Migrations
{
    /// <inheritdoc />
    public partial class CompanyNotRequired : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TournamentApplications_Companies_CompanyId",
                table: "TournamentApplications");

            migrationBuilder.AlterColumn<string>(
                name: "CompanyId",
                table: "TournamentApplications",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddForeignKey(
                name: "FK_TournamentApplications_Companies_CompanyId",
                table: "TournamentApplications",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TournamentApplications_Companies_CompanyId",
                table: "TournamentApplications");

            migrationBuilder.AlterColumn<string>(
                name: "CompanyId",
                table: "TournamentApplications",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TournamentApplications_Companies_CompanyId",
                table: "TournamentApplications",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Name",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
