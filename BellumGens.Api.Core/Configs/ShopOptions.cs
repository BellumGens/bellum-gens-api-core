namespace BellumGens.Api.Core
{
	/// <summary>
	/// Bound from the "shop" configuration section: deployment and payment settings. Shipping prices and the quantity limit
	/// are edited from the admin panel and stored in the database (see <see cref="Models.ShopSettings"/>).
	/// </summary>
	public class ShopOptions
	{
		public const string Section = "shop";

		/// <summary>Name of the rate limiting policy applied to order creation.</summary>
		public const string OrdersRateLimitPolicy = "shop-orders";

		/// <summary>The only country the shop delivers to (ISO 3166-1 alpha-2).</summary>
		public const string DeliveryCountry = "BG";

		/// <summary>Shown when a delivery address outside <see cref="DeliveryCountry"/> is refused.</summary>
		public const string DeliveryCountryMessage = "We currently deliver within Bulgaria only.";

		/// <summary>Bulgarian postal codes have four digits.</summary>
		public const string PostalCodePattern = @"^\d{4}$";

		/// <summary>How many delivery addresses one customer can keep.</summary>
		public const int MaxSavedAddresses = 10;

		public string Currency { get; set; } = "EUR";

		/// <summary>
		/// How long an unpaid order holds its stock before the sweeper cancels it. Each payment attempt opens a
		/// checkout for this long and extends the order's hold to match.
		/// </summary>
		public int PaymentExpiryMinutes { get; set; } = 60;

		/// <summary>
		/// How long past its payment window an order is still kept while the provider reports its payment as in
		/// progress (pending or authorised), so a payment completing at the last moment is not cancelled under it.
		/// </summary>
		public int PendingPaymentGraceMinutes { get; set; } = 15;

		/// <summary>Storefront origin used to build the post-payment redirect URL.</summary>
		public string StorefrontUrl { get; set; } = "https://bellumgens.com";

		/// <summary>Seconds after which a pending payment is re-checked with the provider on read.</summary>
		public int ReconcileAfterSeconds { get; set; } = 30;
	}
}
