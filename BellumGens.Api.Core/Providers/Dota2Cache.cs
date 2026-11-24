using Microsoft.Extensions.Caching.Memory;

namespace BellumGens.Api.Core.Providers
{
	/// <summary>
	/// The cache of the Dota 2 data requested from the Steam API. Anyone can view the profile of any public
	/// steam id, so this cache has a size limit and is kept apart from the shared memory cache: an entry added
	/// here must have a <see cref="ICacheEntry.Size"/>, and when the cache is full the least recently used,
	/// lowest priority entries are dropped instead of the memory growing.
	/// </summary>
	public sealed class Dota2Cache : MemoryCache
	{
		/// <summary>
		/// The size of a cached profile, the unit the other sizes are measured in.
		/// </summary>
		public const long ProfileSize = 1;

		/// <summary>
		/// The size of the details of a match: they hold all ten players, so they're a few times larger than a profile.
		/// </summary>
		public const long MatchSize = 4;

		/// <summary>
		/// The size of the hero list.
		/// </summary>
		public const long HeroesSize = 4;

		/// <summary>
		/// The default size limit: about 2,500 matches, or 10,000 profiles.
		/// </summary>
		public const long DefaultSizeLimit = 10_000;

		public Dota2Cache() : this(DefaultSizeLimit) { }

		public Dota2Cache(long sizeLimit) : base(new MemoryCacheOptions { SizeLimit = sizeLimit }) { }
	}
}
