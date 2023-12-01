using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BellumGens.Api.Core.Migrations
{
    /// <inheritdoc />
    public partial class Clean1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_TeamInvites",
                table: "TeamInvites");

            migrationBuilder.AlterColumn<string>(
                name: "InvitedUserId",
                table: "TeamInvites",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "InvitingUserId",
                table: "TeamInvites",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TeamInvites",
                table: "TeamInvites",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_TeamInvites_InvitingUserId",
                table: "TeamInvites",
                column: "InvitingUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_TeamInvites",
                table: "TeamInvites");

            migrationBuilder.DropIndex(
                name: "IX_TeamInvites_InvitingUserId",
                table: "TeamInvites");

            migrationBuilder.AlterColumn<string>(
                name: "InvitingUserId",
                table: "TeamInvites",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "InvitedUserId",
                table: "TeamInvites",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_TeamInvites",
                table: "TeamInvites",
                columns: new[] { "InvitingUserId", "InvitedUserId", "TeamId" });
        }
    }
}
