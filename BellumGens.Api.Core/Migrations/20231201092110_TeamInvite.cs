using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BellumGens.Api.Core.Migrations
{
    /// <inheritdoc />
    public partial class TeamInvite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_dbo.TeamInvites",
                table: "TeamInvites");

            migrationBuilder.CreateIndex("IX_TeamInvites_InvitedUserId", "TeamInvites", "InvitedUserId");

            migrationBuilder.AlterColumn<string>(
                name: "InvitingUserId",
                table: "TeamInvites",
                type: "nvarchar(450)",
                oldType: "nvarchar(128)");

            migrationBuilder.AlterColumn<string>(
                name: "InvitedUserId",
                table: "TeamInvites",
                type: "nvarchar(450)",
                oldType: "nvarchar(128)");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TeamInvites",
                table: "TeamInvites",
                columns: new[] { "InvitingUserId", "InvitedUserId", "TeamId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_TeamInvites",
                table: "TeamInvites");

            migrationBuilder.AlterColumn<string>(
                name: "InvitedUserId",
                table: "TeamInvites",
                type: "nvarchar(128)",
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "InvitingUserId",
                table: "TeamInvites",
                type: "nvarchar(128)",
                oldType: "nvarchar(450)");

            migrationBuilder.DropIndex("IX_TeamInvites_InvitedUserId", "TeamInvites", "InvitedUserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TeamInvites",
                table: "TeamInvites",
                column: "Id");
        }
    }
}
