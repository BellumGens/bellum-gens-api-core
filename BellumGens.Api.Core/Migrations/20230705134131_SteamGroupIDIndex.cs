using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BellumGens.Api.Core.Migrations
{
    /// <inheritdoc />
    public partial class SteamGroupIDIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_CSGOTeams_SteamGroupId",
                table: "CSGOTeams",
                column: "SteamGroupId",
                unique: true,
                filter: "[SteamGroupId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CSGOTeams_SteamGroupId",
                table: "CSGOTeams");
        }
    }
}
