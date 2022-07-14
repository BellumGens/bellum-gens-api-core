using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BellumGens.Api.Core.Migrations
{
    public partial class SeparateSteamAndBNETUserDetails : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "SteamID",
                table: "AspNetUsers",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BattleNetId",
                table: "AspNetUsers",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "CSGODetails",
                columns: table => new
                {
                    SteamId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AvatarFull = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AvatarMedium = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AvatarIcon = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RealName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Country = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HeadshotPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    KillDeathRatio = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: false),
                    Accuracy = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    SteamPrivate = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CSGODetails", x => x.SteamId);
                });

            migrationBuilder.CreateTable(
                name: "StarCraft2Details",
                columns: table => new
                {
                    BattleNetId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BattleNetBattleTag = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StarCraft2Details", x => x.BattleNetId);
                });

            migrationBuilder.Sql(
                @"INSERT INTO dbo.CSGODetails
                SELECT SteamID, AvatarFull, AvatarMedium, AvatarIcon, RealName, CustomUrl, Country, HeadshotPercentage, KillDeathRatio, Accuracy, SteamPrivate FROM dbo.AspNetUsers
                WHERE SteamID IS NOT NULL"
            );

            migrationBuilder.Sql(
                @"INSERT INTO dbo.StarCraft2Details
                SELECT BattleNetId, BattleNetBattleTag FROM dbo.AspNetUsers
                WHERE BattleNetId IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_BattleNetId",
                table: "AspNetUsers",
                column: "BattleNetId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_SteamID",
                table: "AspNetUsers",
                column: "SteamID");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_CSGODetails_SteamID",
                table: "AspNetUsers",
                column: "SteamID",
                principalTable: "CSGODetails",
                principalColumn: "SteamId",
                onDelete: ReferentialAction.NoAction,
                onUpdate: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_StarCraft2Details_BattleNetId",
                table: "AspNetUsers",
                column: "BattleNetId",
                principalTable: "StarCraft2Details",
                principalColumn: "BattleNetId",
                onDelete: ReferentialAction.NoAction,
                onUpdate: ReferentialAction.Cascade);

            migrationBuilder.DropColumn(
                name: "Accuracy",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AvatarFull",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AvatarIcon",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AvatarMedium",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "BattleNetBattleTag",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CustomUrl",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "HeadshotPercentage",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "KillDeathRatio",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "RealName",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SteamPrivate",
                table: "AspNetUsers");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_CSGODetails_SteamID",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_StarCraft2Details_BattleNetId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_BattleNetId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_SteamID",
                table: "AspNetUsers");

            migrationBuilder.AlterColumn<string>(
                name: "SteamID",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BattleNetId",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Accuracy",
                table: "AspNetUsers",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "AvatarFull",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AvatarIcon",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AvatarMedium",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BattleNetBattleTag",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomUrl",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HeadshotPercentage",
                table: "AspNetUsers",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "KillDeathRatio",
                table: "AspNetUsers",
                type: "decimal(4,2)",
                precision: 4,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "RealName",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SteamPrivate",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                @"INSERT INTO dbo.AspNetUsers AvatarFull, AvatarMedium, AvatarIcon, RealName, CustomUrl, Country, HeadshotPercentage, KillDeathRatio, Accuracy, SteamPrivate
                SELECT AvatarFull, AvatarMedium, AvatarIcon, RealName, CustomUrl, Country, HeadshotPercentage, KillDeathRatio, Accuracy, SteamPrivate FROM dbo.CSGODetails
                WHERE AspNetUsers.SteamId == CSGODetails.SteamId"
            );

            migrationBuilder.Sql(
                @"INSERT INTO dbo.AspNetUsers BattleNetBattleTag
                SELECT BattleNetBattleTag FROM dbo.StarCraft2Details
                WHERE AspNetUsers.BattleNetId == StarCraft2Details.BattleNetId"
            );

            migrationBuilder.DropTable(
                name: "CSGODetails");

            migrationBuilder.DropTable(
                name: "StarCraft2Details");
        }
    }
}
