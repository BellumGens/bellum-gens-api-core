using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BellumGens.Api.Core.Migrations
{
    /// <inheritdoc />
    public partial class Orders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "JerseyDetails",
                newName: "OrderDetails");

            migrationBuilder.RenameTable(
                name: "JerseyOrders",
                newName: "Orders");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "OrderDetails",
                newName: "JerseyDetails");

            migrationBuilder.RenameTable(
                name: "Orders",
                newName: "JerseyOrders");
        }
    }
}
