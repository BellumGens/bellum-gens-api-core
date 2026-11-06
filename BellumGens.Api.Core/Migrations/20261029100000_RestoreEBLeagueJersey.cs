using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BellumGens.Api.Core.Migrations
{
    /// <inheritdoc />
    public partial class RestoreEBLeagueJersey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ShopCatalogAndOrders migrated the EB League jersey as an inactive product with only the variants that had
            // legacy orders (none on a fresh database). Put it back on sale at 30 EUR with every cut/size.
            // The SQLite development database never runs migrations; SqliteDevelopmentDatabase.EnsureShopCatalog (Program.cs) seeds it there.
            migrationBuilder.Sql(@"
DECLARE @productId uniqueidentifier = (SELECT [Id] FROM [Products] WHERE [Slug] = N'esports-business-league-jersey');
DECLARE @now datetimeoffset = SYSDATETIMEOFFSET();
DECLARE @imageUrl nvarchar(max) = N'https://eb-league.com/assets/wallpapers/jersey-front-back.webp';

IF @productId IS NULL
BEGIN
    SET @productId = NEWID();
    INSERT INTO [Products] ([Id], [Name], [Slug], [Description], [Type], [Brand], [Price], [DiscountPercentage], [ImageUrl], [GalleryUrls], [StockQuantity], [Active], [SortOrder], [CreatedOn], [UpdatedOn])
    VALUES (@productId, N'Esports Business League Jersey', N'esports-business-league-jersey',
            N'Limited edition Esports Business League jersey made of 100% breathable sportswear polyester with a high quality print.',
            0 /* Jersey */, 1 /* EBLeague */, 30.00, NULL, @imageUrl, N'[]', NULL, 1, 0, @now, @now);
END
ELSE
BEGIN
    UPDATE [Products]
    SET [Active] = 1,
        [Price] = 30.00,
        [DiscountPercentage] = NULL,
        [ImageUrl] = ISNULL([ImageUrl], @imageUrl),
        [UpdatedOn] = @now
    WHERE [Id] = @productId;
END

INSERT INTO [ProductVariants] ([Id], [ProductId], [Name], [Cut], [Size], [Sku], [PriceOverride], [StockQuantity], [Active], [SortOrder])
SELECT NEWID(), @productId, c.[Name] + N' / ' + s.[Name], c.[Cut], s.[Size], NULL, NULL, NULL, 1, c.[Cut] * 10 + s.[Size]
FROM (VALUES (0, N'Male'), (1, N'Female')) c([Cut], [Name])
CROSS JOIN (VALUES (0, N'XS'), (1, N'S'), (2, N'M'), (3, N'L'), (4, N'XL'), (5, N'XXL'), (6, N'XXXL')) s([Size], [Name])
WHERE NOT EXISTS (SELECT 1 FROM [ProductVariants] v WHERE v.[ProductId] = @productId AND v.[Cut] = c.[Cut] AND v.[Size] = s.[Size]);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Variants may already be referenced by order lines, so only take the product off sale.
            migrationBuilder.Sql(@"
UPDATE [Products] SET [Active] = 0, [UpdatedOn] = SYSDATETIMEOFFSET() WHERE [Slug] = N'esports-business-league-jersey';
");
        }
    }
}
