using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BellumGens.Api.Core.Migrations
{
    /// <inheritdoc />
    public partial class ShopCatalogAndOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing promo codes stay usable after the upgrade.
            migrationBuilder.AddColumn<bool>(
                name: "Active",
                table: "PromoCodes",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "Brand",
                table: "PromoCodes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumOrderTotal",
                table: "PromoCodes",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TimesUsed",
                table: "PromoCodes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UsageLimit",
                table: "PromoCodes",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Brand = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    DiscountPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    ImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GalleryUrls = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StockQuantity = table.Column<int>(type: "int", nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShopOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderSequence = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    City = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StreetAddress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PostalCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Country = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PromoCode = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    DeliveryMethod = table.Column<int>(type: "int", nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    DiscountTotal = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    ShippingCost = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PaymentMethod = table.Column<int>(type: "int", nullable: false),
                    CustomerNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AdminNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TrackingNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OrderDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PaidOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ShippedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExpiresOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShopOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShopOrders_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ShopOrders_PromoCodes_PromoCode",
                        column: x => x.PromoCode,
                        principalTable: "PromoCodes",
                        principalColumn: "Code");
                });

            migrationBuilder.CreateTable(
                name: "ProductVariants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Cut = table.Column<int>(type: "int", nullable: true),
                    Size = table.Column<int>(type: "int", nullable: true),
                    Sku = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PriceOverride = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    StockQuantity = table.Column<int>(type: "int", nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductVariants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductVariants_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<int>(type: "int", nullable: false),
                    ProviderOrderId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ProviderToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CheckoutUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastEventType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastPayload = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_ShopOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "ShopOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VariantName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnitPrice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderItems_ProductVariants_VariantId",
                        column: x => x.VariantId,
                        principalTable: "ProductVariants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OrderItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OrderItems_ShopOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "ShopOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OrderId",
                table: "OrderItems",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_ProductId",
                table: "OrderItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_VariantId",
                table: "OrderItems",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrderId",
                table: "Payments",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ProviderOrderId",
                table: "Payments",
                column: "ProviderOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Slug",
                table: "Products",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_ProductId",
                table: "ProductVariants",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ShopOrders_OrderSequence",
                table: "ShopOrders",
                column: "OrderSequence",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShopOrders_PromoCode",
                table: "ShopOrders",
                column: "PromoCode");

            migrationBuilder.CreateIndex(
                name: "IX_ShopOrders_UserId",
                table: "ShopOrders",
                column: "UserId");

            // Move the legacy jersey orders into the new tables before their tables go away.
            // The legacy shop sold one product (the EB League jersey) at 60 BGN with a 30% base discount plus the
            // promo discount, cash on delivery. Totals are reproduced in BGN so the history stays truthful; the
            // migrated product is created inactive so it can be re-priced in EUR before it shows in the shop.
            migrationBuilder.Sql(@"
DECLARE @productId uniqueidentifier = NEWID();
DECLARE @now datetimeoffset = SYSDATETIMEOFFSET();

INSERT INTO [Products] ([Id], [Name], [Slug], [Description], [Type], [Brand], [Price], [DiscountPercentage], [ImageUrl], [GalleryUrls], [StockQuantity], [Active], [SortOrder], [CreatedOn], [UpdatedOn])
VALUES (@productId, N'Esports Business League Jersey', N'esports-business-league-jersey',
        N'Limited edition Esports Business League jersey made of 100% breathable sportswear polyester with a high quality print.',
        0 /* Jersey */, 1 /* EBLeague */, 30.68, NULL, NULL, N'[]', NULL, 0, 0, @now, @now);

INSERT INTO [ProductVariants] ([Id], [ProductId], [Name], [Cut], [Size], [Sku], [PriceOverride], [StockQuantity], [Active], [SortOrder])
SELECT NEWID(), @productId,
       CASE d.[Cut] WHEN 0 THEN N'Male' ELSE N'Female' END + N' / ' +
       CASE d.[Size] WHEN 0 THEN N'XS' WHEN 1 THEN N'S' WHEN 2 THEN N'M' WHEN 3 THEN N'L' WHEN 4 THEN N'XL' WHEN 5 THEN N'XXL' ELSE N'XXXL' END,
       d.[Cut], d.[Size], NULL, NULL, NULL, 1, d.[Cut] * 10 + d.[Size]
FROM (SELECT DISTINCT [Cut], [Size] FROM [JerseyDetails]) d;

INSERT INTO [ShopOrders] ([Id], [UserId], [Email], [FirstName], [LastName], [PhoneNumber], [City], [StreetAddress], [PostalCode], [Country], [Language],
                          [PromoCode], [DeliveryMethod], [Subtotal], [DiscountTotal], [ShippingCost], [Total], [Currency], [Status], [PaymentMethod],
                          [CustomerNote], [AdminNote], [TrackingNumber], [OrderDate], [PaidOn], [ShippedOn], [ExpiresOn])
SELECT o.[Id], NULL, o.[Email], o.[FirstName], o.[LastName], o.[PhoneNumber], o.[City], o.[StreetAddress], NULL, N'BG', N'bg',
       o.[PromoCode], 0 /* Courier */,
       c.[Cnt] * 60.00,
       ROUND(c.[Cnt] * 60.00 * (0.30 + ISNULL(p.[Discount], 0)), 2),
       0.00,
       ROUND(c.[Cnt] * 60.00 * (1 - 0.30 - ISNULL(p.[Discount], 0)), 2),
       N'BGN',
       CASE WHEN o.[Shipped] = 1 THEN 2 /* Shipped */ WHEN o.[Confirmed] = 1 THEN 1 /* Paid */ ELSE 4 /* Cancelled */ END,
       1 /* CashOnDelivery */,
       NULL, N'Migrated from the legacy jersey shop.', NULL,
       o.[OrderDate],
       CASE WHEN o.[Confirmed] = 1 OR o.[Shipped] = 1 THEN o.[OrderDate] END,
       CASE WHEN o.[Shipped] = 1 THEN o.[OrderDate] END,
       o.[OrderDate]
FROM [JerseyOrders] o
LEFT JOIN [PromoCodes] p ON p.[Code] = o.[PromoCode]
CROSS APPLY (SELECT COUNT(*) AS [Cnt] FROM [JerseyDetails] d WHERE d.[OrderId] = o.[Id]) c;

INSERT INTO [OrderItems] ([OrderId], [ProductId], [VariantId], [ProductName], [VariantName], [UnitPrice], [Quantity], [LineTotal])
SELECT d.[OrderId], @productId, v.[Id], N'Esports Business League Jersey', v.[Name], 60.00, 1, 60.00
FROM [JerseyDetails] d
INNER JOIN [ProductVariants] v ON v.[ProductId] = @productId AND v.[Cut] = d.[Cut] AND v.[Size] = d.[Size];
");

            migrationBuilder.DropTable(
                name: "JerseyDetails");

            migrationBuilder.DropTable(
                name: "JerseyOrders");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderItems");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "ProductVariants");

            migrationBuilder.DropTable(
                name: "ShopOrders");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropColumn(
                name: "Active",
                table: "PromoCodes");

            migrationBuilder.DropColumn(
                name: "Brand",
                table: "PromoCodes");

            migrationBuilder.DropColumn(
                name: "MinimumOrderTotal",
                table: "PromoCodes");

            migrationBuilder.DropColumn(
                name: "TimesUsed",
                table: "PromoCodes");

            migrationBuilder.DropColumn(
                name: "UsageLimit",
                table: "PromoCodes");

            migrationBuilder.CreateTable(
                name: "JerseyOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PromoCode = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    City = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Confirmed = table.Column<bool>(type: "bit", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OrderDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Shipped = table.Column<bool>(type: "bit", nullable: false),
                    StreetAddress = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JerseyOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JerseyOrders_PromoCodes_PromoCode",
                        column: x => x.PromoCode,
                        principalTable: "PromoCodes",
                        principalColumn: "Code");
                });

            migrationBuilder.CreateTable(
                name: "JerseyDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Cut = table.Column<int>(type: "int", nullable: false),
                    Size = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JerseyDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JerseyDetails_JerseyOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "JerseyOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JerseyDetails_OrderId",
                table: "JerseyDetails",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_JerseyOrders_PromoCode",
                table: "JerseyOrders",
                column: "PromoCode");
        }
    }
}
