using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace BellumGens.Api.Core.Migrations
{
    /// <inheritdoc />
    public partial class TeamApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TeamApplications_AspNetUsers_ApplicantId",
                table: "TeamApplications");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TeamApplications",
                table: "TeamApplications");

            migrationBuilder.AlterColumn<string>(
                name: "ApplicantId",
                table: "TeamApplications",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "TeamApplications",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TeamApplications",
                table: "TeamApplications",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_TeamApplications_ApplicantId",
                table: "TeamApplications",
                column: "ApplicantId");

            migrationBuilder.AddForeignKey(
                name: "FK_TeamApplications_AspNetUsers_ApplicantId",
                table: "TeamApplications",
                column: "ApplicantId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TeamApplications_AspNetUsers_ApplicantId",
                table: "TeamApplications");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TeamApplications",
                table: "TeamApplications");

            migrationBuilder.DropIndex(
                name: "IX_TeamApplications_ApplicantId",
                table: "TeamApplications");

            migrationBuilder.AlterColumn<string>(
                name: "ApplicantId",
                table: "TeamApplications",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "Id",
                table: "TeamApplications");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TeamApplications",
                table: "TeamApplications",
                columns: new[] { "ApplicantId", "TeamId" });

            migrationBuilder.AddForeignKey(
                name: "FK_TeamApplications_AspNetUsers_ApplicantId",
                table: "TeamApplications",
                column: "ApplicantId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
