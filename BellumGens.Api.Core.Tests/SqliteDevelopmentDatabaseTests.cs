#if SQLITE_PROVIDER
using BellumGens.Api.Core.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BellumGens.Api.Core.Tests;

public class SqliteDevelopmentDatabaseTests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void RebuildsWhenSchemaChangesOrRecordIsMissing(bool removeRecord)
	{
		var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.dev.db");
		var options = new DbContextOptionsBuilder<BellumGensDbContext>()
			.UseSqlite($"Data Source={databasePath}")
			.Options;

		try
		{
			using (var context = new BellumGensDbContext(options))
			{
				SqliteDevelopmentDatabase.EnsureCurrentModel(context);
				context.Roles.Add(new IdentityRole("Example"));
				context.SaveChanges();
			}

			using (var context = new BellumGensDbContext(options))
			{
				SqliteDevelopmentDatabase.EnsureCurrentModel(context);
				Assert.Single(context.Roles);

				if (removeRecord)
					context.Database.ExecuteSqlRaw("DROP TABLE \"__DevSchema\"");
				else
					context.Database.ExecuteSqlRaw("UPDATE \"__DevSchema\" SET \"Hash\" = 'old-model'");
			}

			using (var context = new BellumGensDbContext(options))
			{
				SqliteDevelopmentDatabase.EnsureCurrentModel(context);
				Assert.Empty(context.Roles);
			}
		}
		finally
		{
			using var context = new BellumGensDbContext(options);
			context.Database.EnsureDeleted();
		}
	}

	[Fact]
	public async Task OrdersDateTimeOffsetsByInstant()
	{
		var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.dev.db");
		var options = new DbContextOptionsBuilder<BellumGensDbContext>()
			.UseSqlite($"Data Source={databasePath}")
			.Options;
		var earlier = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.FromHours(2));
		var later = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

		try
		{
			using (var context = new BellumGensDbContext(options))
			{
				context.Database.EnsureCreated();
				context.CSGOStrategies.AddRange(
					new CSGOStrategy { Title = "Earlier", LastUpdated = earlier },
					new CSGOStrategy { Title = "Later", LastUpdated = later });
				context.PromoCodes.AddRange(
					new Promo { Code = "Expiring", Expiration = earlier },
					new Promo { Code = "NoExpiration", Expiration = null });
				await context.SaveChangesAsync(TestContext.Current.CancellationToken);
			}

			using (var context = new BellumGensDbContext(options))
			{
				var strategies = await context.CSGOStrategies
					.OrderByDescending(strategy => strategy.LastUpdated)
					.ToListAsync(TestContext.Current.CancellationToken);

				Assert.Equal(["Later", "Earlier"], strategies.Select(strategy => strategy.Title));
				Assert.Equal(earlier, await context.PromoCodes
					.Where(promo => promo.Expiration != null)
					.Select(promo => promo.Expiration)
					.SingleAsync(TestContext.Current.CancellationToken));
			}
		}
		finally
		{
			using var context = new BellumGensDbContext(options);
			context.Database.EnsureDeleted();
		}
	}
}
#endif
