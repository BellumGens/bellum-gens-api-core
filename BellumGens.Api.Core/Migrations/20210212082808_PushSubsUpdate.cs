using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace BellumGens.Api.Core.Migrations
{
    public partial class PushSubsUpdate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "userId",
                table: "BellumGensPushSubscriptions",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "expirationTime",
                table: "BellumGensPushSubscriptions",
                newName: "ExpirationTime");

            migrationBuilder.RenameColumn(
                name: "endpoint",
                table: "BellumGensPushSubscriptions",
                newName: "Endpoint");

            migrationBuilder.RenameColumn(
                name: "auth",
                table: "BellumGensPushSubscriptions",
                newName: "Auth");

            migrationBuilder.RenameColumn(
                name: "p256dh",
                table: "BellumGensPushSubscriptions",
                newName: "P256dh");

            migrationBuilder.AlterColumn<TimeSpan>(
                name: "ExpirationTime",
                table: "BellumGensPushSubscriptions",
                type: "time",
                nullable: true,
                oldClrType: typeof(TimeSpan),
                oldType: "time");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "BellumGensPushSubscriptions",
                newName: "userId");

            migrationBuilder.RenameColumn(
                name: "ExpirationTime",
                table: "BellumGensPushSubscriptions",
                newName: "expirationTime");

            migrationBuilder.RenameColumn(
                name: "Endpoint",
                table: "BellumGensPushSubscriptions",
                newName: "endpoint");

            migrationBuilder.RenameColumn(
                name: "Auth",
                table: "BellumGensPushSubscriptions",
                newName: "auth");

            migrationBuilder.RenameColumn(
                name: "P256dh",
                table: "BellumGensPushSubscriptions",
                newName: "p256dh");

            migrationBuilder.AlterColumn<TimeSpan>(
                name: "expirationTime",
                table: "BellumGensPushSubscriptions",
                type: "time",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0),
                oldClrType: typeof(TimeSpan),
                oldType: "time",
                oldNullable: true);
        }
    }
}
