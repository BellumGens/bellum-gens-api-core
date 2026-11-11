using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace BellumGens.Api.Core.Models
{
	/// <summary>
	/// Shop pricing rules the admin edits from the admin panel. The table holds a single row; deployment and payment
	/// settings (currency, payment window, storefront URL) stay in the "shop" configuration section.
	/// </summary>
	public class ShopSettings
	{
		/// <summary>The id of the only row.</summary>
		public const int SingletonId = 1;

		public const decimal DefaultShippingCost = 6.00M;
		public const decimal DefaultFreeShippingThreshold = 50.00M;
		public const int DefaultMaxQuantityPerLine = 10;

		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.None)]
		[JsonIgnore]
		public int Id { get; set; } = SingletonId;

		/// <summary>Courier fee charged below the free shipping threshold.</summary>
		[Range(typeof(decimal), "0", "1000")]
		public decimal ShippingCost { get; set; } = DefaultShippingCost;

		/// <summary>Orders whose discounted subtotal reaches this amount ship for free.</summary>
		[Range(typeof(decimal), "0", "100000")]
		public decimal FreeShippingThreshold { get; set; } = DefaultFreeShippingThreshold;

		/// <summary>The most units of one product or variant a single cart line can hold.</summary>
		[Range(1, 100)]
		public int MaxQuantityPerLine { get; set; } = DefaultMaxQuantityPerLine;

		public DateTimeOffset UpdatedOn { get; set; } = DateTimeOffset.Now;

		/// <summary>The admin who last changed the settings; null for the row created at startup.</summary>
		[MaxLength(450)]
		public string UpdatedBy { get; set; }

		public ShopSettings Copy()
		{
			return (ShopSettings)MemberwiseClone();
		}
	}
}
