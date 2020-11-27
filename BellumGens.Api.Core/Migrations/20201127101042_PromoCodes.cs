using Microsoft.EntityFrameworkCore.Migrations;

namespace BellumGens.Api.Core.Migrations
{
    public partial class PromoCodes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_dbo.JerseyOrders_dbo.Promoes_PromoCode",
                table: "JerseyOrders");

            migrationBuilder.DropPrimaryKey(
                name: "PK_dbo.Promoes",
                table: "Promoes");

            migrationBuilder.RenameTable(
                name: "Promoes",
                newName: "PromoCodes");

            migrationBuilder.RenameIndex(
                name: "IX_Code",
                table: "PromoCodes",
                newName: "IX_PromoCodes_Code");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PromoCodes",
                table: "PromoCodes",
                column: "Code");

            migrationBuilder.AddForeignKey(
                name: "FK_JerseyOrders_PromoCodes_PromoCode",
                table: "JerseyOrders",
                column: "PromoCode",
                principalTable: "PromoCodes",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JerseyOrders_PromoCodes_PromoCode",
                table: "JerseyOrders");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PromoCodes",
                table: "PromoCodes");

            migrationBuilder.RenameTable(
                name: "PromoCodes",
                newName: "Promoes");

            migrationBuilder.RenameIndex(
                name: "IX_PromoCodes_Code",
                table: "Promoes",
                newName: "IX_Promoes_Code");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Promoes",
                table: "Promoes",
                column: "Code");

            migrationBuilder.AddForeignKey(
                name: "FK_JerseyOrders_Promoes_PromoCode",
                table: "JerseyOrders",
                column: "PromoCode",
                principalTable: "Promoes",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
