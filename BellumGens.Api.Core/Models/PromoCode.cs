using System;
using System.ComponentModel.DataAnnotations;

namespace BellumGens.Api.Core.Models
{
	public class Promo
	{
		[Key]
		[Required]
		public string Code { get; set; }

		/// <summary>Fraction of the eligible subtotal, e.g. 0.10 for 10% off.</summary>
		[Range(0, 1)]
		public decimal Discount { get; set; }

		public DateTimeOffset? Expiration { get; set; }

		public bool Active { get; set; } = true;

		/// <summary>Maximum number of orders that may use the code. Null means unlimited.</summary>
		public int? UsageLimit { get; set; }

		/// <summary>Orders using the code: paid ones plus those still awaiting payment.</summary>
		[ConcurrencyCheck]
		public int TimesUsed { get; set; }

		/// <summary>Subtotal (before discount) required for the code to apply. Null means no minimum.</summary>
		public decimal? MinimumOrderTotal { get; set; }

		/// <summary>When set, the discount only applies to lines from this brand.</summary>
		public Brand? Brand { get; set; }

		public bool IsUsable(DateTimeOffset now)
		{
			return Active
				&& (Expiration == null || Expiration > now)
				&& (UsageLimit == null || TimesUsed < UsageLimit);
		}
	}
}
