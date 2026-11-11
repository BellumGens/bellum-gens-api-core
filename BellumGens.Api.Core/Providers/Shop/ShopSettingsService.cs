using BellumGens.Api.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BellumGens.Api.Core.Providers
{
	/// <summary>Reads and changes the admin-editable <see cref="ShopSettings"/>.</summary>
	public interface IShopSettingsService
	{
		/// <summary>The current settings, or the defaults when none are stored. Returns a copy the caller may change.</summary>
		Task<ShopSettings> GetAsync(CancellationToken cancellationToken = default);

		/// <summary>Stores the given values and returns the saved settings.</summary>
		Task<ShopSettings> UpdateAsync(ShopSettings settings, string userId, CancellationToken cancellationToken = default);
	}

	public class ShopSettingsService : IShopSettingsService
	{
		public const string CacheKey = "shop:settings";

		/// <summary>
		/// Saving clears the cache only on the instance that saved, so another instance may price with the old values for
		/// up to this long.
		/// </summary>
		public static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1);

		private readonly BellumGensDbContext _db;
		private readonly IMemoryCache _cache;

		public ShopSettingsService(BellumGensDbContext db, IMemoryCache cache)
		{
			_db = db;
			_cache = cache;
		}

		public async Task<ShopSettings> GetAsync(CancellationToken cancellationToken = default)
		{
			if (!_cache.TryGetValue(CacheKey, out ShopSettings settings))
			{
				settings = await _db.ShopSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == ShopSettings.SingletonId, cancellationToken)
					?? new ShopSettings();
				_cache.Set(CacheKey, settings, CacheDuration);
			}
			return settings.Copy();
		}

		public async Task<ShopSettings> UpdateAsync(ShopSettings settings, string userId, CancellationToken cancellationToken = default)
		{
			ShopSettings entity = await _db.ShopSettings.FirstOrDefaultAsync(s => s.Id == ShopSettings.SingletonId, cancellationToken);
			if (entity == null)
			{
				entity = new ShopSettings();
				_db.ShopSettings.Add(entity);
			}
			entity.ShippingCost = OrderService.Round(settings.ShippingCost);
			entity.FreeShippingThreshold = OrderService.Round(settings.FreeShippingThreshold);
			entity.MaxQuantityPerLine = settings.MaxQuantityPerLine;
			entity.UpdatedOn = DateTimeOffset.Now;
			entity.UpdatedBy = userId;
			await _db.SaveChangesAsync(cancellationToken);
			_cache.Remove(CacheKey);
			return entity.Copy();
		}

		/// <summary>
		/// Creates the settings row on first start. It takes the values from the "shop" configuration section, where these
		/// settings lived before they moved to the database, so an environment keeps the values it was configured with.
		/// </summary>
		public static void EnsureCreated(BellumGensDbContext db, IConfiguration configuration)
		{
			if (db.ShopSettings.Any(s => s.Id == ShopSettings.SingletonId))
			{
				return;
			}
			IConfigurationSection legacy = configuration.GetSection(ShopOptions.Section);
			db.ShopSettings.Add(new ShopSettings
			{
				ShippingCost = legacy.GetValue("shippingCost", ShopSettings.DefaultShippingCost),
				FreeShippingThreshold = legacy.GetValue("freeShippingThreshold", ShopSettings.DefaultFreeShippingThreshold),
				MaxQuantityPerLine = legacy.GetValue("maxQuantityPerLine", ShopSettings.DefaultMaxQuantityPerLine)
			});
			try
			{
				db.SaveChanges();
			}
			catch (DbUpdateException)
			{
				// Another instance starting at the same time created the row first.
				db.ChangeTracker.Clear();
			}
		}
	}
}
