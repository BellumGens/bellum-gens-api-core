using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace BellumGens.Api.Core.Models
{
	public class Product
	{
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public Guid Id { get; set; }

		[Required]
		public string Name { get; set; }

		/// <summary>
		/// URL-friendly unique identifier used by the storefront product page. Optional on input: the admin
		/// endpoints generate it from the name when it is left blank. Required in the database (see ConfigureShop).
		/// </summary>
		public string Slug { get; set; }

		public string Description { get; set; }

		public ProductType Type { get; set; }

		public Brand Brand { get; set; }

		/// <summary>Price in the shop currency (EUR), before any product discount.</summary>
		[Range(0, 100000)]
		public decimal Price { get; set; }

		/// <summary>Percentage discount shown with a strike-through price. 0-100.</summary>
		[Range(0, 100)]
		public decimal? DiscountPercentage { get; set; }

		public string ImageUrl { get; set; }

		public List<string> GalleryUrls { get; set; } = new();

		/// <summary>Stock for products without variants. Null means stock is not tracked.</summary>
		public int? StockQuantity { get; set; }

		/// <summary>
		/// Write-only: the stock the admin editor loaded. An update applies the change from this value instead of
		/// overwriting the stock, so units that orders took or released while the editor was open are kept.
		/// </summary>
		[NotMapped]
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
		public int? OriginalStockQuantity { get; set; }

		public bool Active { get; set; } = true;

		public int SortOrder { get; set; }

		public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.Now;

		public DateTimeOffset UpdatedOn { get; set; } = DateTimeOffset.Now;

		[Timestamp]
		[JsonIgnore]
		public byte[] RowVersion { get; set; }

		public virtual ICollection<ProductVariant> Variants { get; set; } = new HashSet<ProductVariant>();

		/// <summary>Unit price after the product discount, rounded to cents.</summary>
		public decimal EffectivePrice(ProductVariant variant = null)
		{
			decimal basePrice = variant?.PriceOverride ?? Price;
			if (DiscountPercentage is > 0)
			{
				basePrice *= 1 - DiscountPercentage.Value / 100;
			}
			return Math.Round(basePrice, 2, MidpointRounding.AwayFromZero);
		}
	}
}
