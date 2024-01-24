using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BellumGens.Api.Core.Migrations
{
    /// <inheritdoc />
    public partial class Clean : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "TeamInvites",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.CreateIndex(
                name: "IX_TeamInvites_TeamId",
                table: "TeamInvites",
                column: "TeamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TeamInvites_TeamId",
                table: "TeamInvites");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "TeamInvites");
        }
    }
}
