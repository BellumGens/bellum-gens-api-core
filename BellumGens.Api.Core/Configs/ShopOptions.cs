namespace BellumGens.Api.Core
{
	/// <summary>Bound from the "shop" configuration section.</summary>
	public class ShopOptions
	{
		public const string Section = "shop";

		/// <summary>Name of the rate limiting policy applied to order creation.</summary>
		public const string OrdersRateLimitPolicy = "shop-orders";

		public string Currency { get; set; } = "EUR";

		/// <summary>Courier fee charged below the free shipping threshold.</summary>
		public decimal ShippingCost { get; set; } = 6.00M;

		/// <summary>Orders whose discounted subtotal reaches this amount ship for free.</summary>
		public decimal FreeShippingThreshold { get; set; } = 50.00M;

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

		public int MaxQuantityPerLine { get; set; } = 10;

		/// <summary>Storefront origin used to build the post-payment redirect URL.</summary>
		public string StorefrontUrl { get; set; } = "https://bellumgens.com";

		/// <summary>Seconds after which a pending payment is re-checked with the provider on read.</summary>
		public int ReconcileAfterSeconds { get; set; } = 30;
	}
}
