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
}
#endif
