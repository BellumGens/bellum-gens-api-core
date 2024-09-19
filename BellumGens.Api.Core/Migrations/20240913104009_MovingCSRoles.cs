using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BellumGens.Api.Core.Migrations
{
    /// <inheritdoc />
    public partial class MovingCSRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PreferredPrimaryRole",
                table: "CSGODetails",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PreferredSecondaryRole",
                table: "CSGODetails",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(@"UPDATE CSGODetails 
                SET PreferredPrimaryRole = (
                    SELECT PreferredPrimaryRole
                    FROM AspNetUsers
                    WHERE CSGODetails.SteamId = AspNetUsers.SteamId 
                ),
                PreferredSecondaryRole = (
                    SELECT PreferredSecondaryRole
                    FROM AspNetUsers
                    WHERE CSGODetails.SteamId = AspNetUsers.SteamId
                )");

            migrationBuilder.DropColumn(
                name: "PreferredPrimaryRole",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PreferredSecondaryRole",
                table: "AspNetUsers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PreferredPrimaryRole",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PreferredSecondaryRole",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(@"UPDATE AspNetUsers 
                SET PreferredPrimaryRole = (
                    SELECT PreferredPrimaryRole
                    FROM CSGODetails
                    WHERE CSGODetails.SteamId = AspNetUsers.SteamId 
                ),
                PreferredSecondaryRole = (
                    SELECT PreferredSecondaryRole
                    FROM CSGODetails
                    WHERE CSGODetails.SteamId = AspNetUsers.SteamId
                )");

            migrationBuilder.DropColumn(
                name: "PreferredPrimaryRole",
                table: "CSGODetails");

            migrationBuilder.DropColumn(
                name: "PreferredSecondaryRole",
                table: "CSGODetails");
        }
    }
}
