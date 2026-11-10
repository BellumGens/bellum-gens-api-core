using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace BellumGens.Api.Core.Models
{
	public class CartLineRequest
	{
		[Required]
		public Guid ProductId { get; set; }

		public Guid? VariantId { get; set; }

		[Range(1, 100)]
		public int Quantity { get; set; } = 1;
	}

	public class QuoteRequest
	{
		[Required]
		[MinLength(1)]
		public List<CartLineRequest> Lines { get; set; } = new();

		public string PromoCode { get; set; }

		public DeliveryMethod DeliveryMethod { get; set; } = DeliveryMethod.Courier;
	}

	public class CreateOrderRequest : QuoteRequest, IValidatableObject
	{
		[Required]
		[EmailAddress]
		public string Email { get; set; }

		[Required]
		public string FirstName { get; set; }

		[Required]
		public string LastName { get; set; }

		[Required]
		public string PhoneNumber { get; set; }

		/// <summary>Required for courier delivery; ignored for event pickup.</summary>
		public string City { get; set; }

		/// <summary>Required for courier delivery; ignored for event pickup.</summary>
		public string StreetAddress { get; set; }

		public string PostalCode { get; set; }

		public string Country { get; set; } = "BG";

		public string Language { get; set; } = "bg";

		[MaxLength(1000)]
		public string CustomerNote { get; set; }

		[Range(typeof(bool), "true", "true", ErrorMessage = "The terms and conditions must be accepted.")]
		public bool AcceptedTerms { get; set; }

		/// <summary>Online with Revolut, or cash on delivery (courier delivery only).</summary>
		[EnumDataType(typeof(PaymentMethod))]
		public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Revolut;

		/// <summary>
		/// The unpaid order placed earlier from the same cart, which this order replaces (e.g. after the customer switched
		/// the payment method or changed their details). Its stock is released, so one cart never holds stock twice.
		/// </summary>
		public Guid? ReplacesOrderId { get; set; }

		/// <summary>
		/// Keep the delivery address in the signed-in customer's address book for later orders (or mark a saved one as
		/// just used). Ignored for guests and for event pickup.
		/// </summary>
		public bool SaveAddress { get; set; }

		public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
		{
			if (DeliveryMethod != DeliveryMethod.Courier)
			{
				if (PaymentMethod == PaymentMethod.CashOnDelivery)
				{
					yield return new ValidationResult("Cash on delivery is only available for courier delivery.", new[] { nameof(PaymentMethod) });
				}
				yield break;
			}
			if (string.IsNullOrWhiteSpace(City))
			{
				yield return new ValidationResult("The City field is required for courier delivery.", new[] { nameof(City) });
			}
			if (string.IsNullOrWhiteSpace(StreetAddress))
			{
				yield return new ValidationResult("The StreetAddress field is required for courier delivery.", new[] { nameof(StreetAddress) });
			}
			// Several Bulgarian towns share a name; the postal code tells the courier which one.
			if (string.IsNullOrWhiteSpace(PostalCode))
			{
				yield return new ValidationResult("The PostalCode field is required for courier delivery.", new[] { nameof(PostalCode) });
			}
			foreach (ValidationResult result in ShippingAddressRequest.ValidateBulgarian(Country, PostalCode))
			{
				yield return result;
			}
		}
	}

	/// <summary>A delivery address a signed-in customer saves to their address book. Delivery is within Bulgaria only.</summary>
	public class ShippingAddressRequest : IValidatableObject
	{
		[Required]
		[MaxLength(100)]
		public string FirstName { get; set; }

		[Required]
		[MaxLength(100)]
		public string LastName { get; set; }

		[Required]
		[MaxLength(30)]
		public string PhoneNumber { get; set; }

		[Required]
		[MaxLength(100)]
		public string City { get; set; }

		[Required]
		[MaxLength(200)]
		public string StreetAddress { get; set; }

		/// <summary>Required: several Bulgarian towns share a name, and the postal code tells them apart.</summary>
		[Required]
		[MaxLength(10)]
		public string PostalCode { get; set; }

		public string Country { get; set; } = ShopOptions.DeliveryCountry;

		public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
		{
			return ValidateBulgarian(Country, PostalCode);
		}

		/// <summary>
		/// Refuses a country other than Bulgaria and a postal code that is not four digits. An empty country means Bulgaria;
		/// whether the postal code is required is up to the caller.
		/// </summary>
		public static IEnumerable<ValidationResult> ValidateBulgarian(string country, string postalCode)
		{
			if (!string.IsNullOrWhiteSpace(country)
				&& !string.Equals(country.Trim(), ShopOptions.DeliveryCountry, StringComparison.OrdinalIgnoreCase))
			{
				yield return new ValidationResult(ShopOptions.DeliveryCountryMessage, new[] { "Country" });
			}
			if (!string.IsNullOrWhiteSpace(postalCode)
				&& !System.Text.RegularExpressions.Regex.IsMatch(postalCode.Trim(), ShopOptions.PostalCodePattern))
			{
				yield return new ValidationResult("A Bulgarian postal code has 4 digits.", new[] { "PostalCode" });
			}
		}
	}

	/// <summary>What the storefront may see about a promo code. Usage counters stay internal.</summary>
	public class PromoView
	{
		public string Code { get; set; }
		public decimal Discount { get; set; }
		public DateTimeOffset? Expiration { get; set; }
		public decimal? MinimumOrderTotal { get; set; }
		public Brand? Brand { get; set; }
	}

	public class QuoteLine
	{
		public Guid ProductId { get; set; }
		public Guid? VariantId { get; set; }
		public string ProductName { get; set; }
		public string VariantName { get; set; }
		public string ImageUrl { get; set; }
		public decimal UnitPrice { get; set; }
		public int Quantity { get; set; }
		public decimal LineTotal { get; set; }
		/// <summary>Remaining stock when tracked, null otherwise.</summary>
		public int? Available { get; set; }
		/// <summary>Set when the line cannot be fulfilled as requested.</summary>
		public string Problem { get; set; }
	}

	public class QuoteResult
	{
		public List<QuoteLine> Lines { get; set; } = new();
		public decimal Subtotal { get; set; }
		public decimal DiscountTotal { get; set; }
		public decimal ShippingCost { get; set; }
		public decimal Total { get; set; }
		public string Currency { get; set; }
		public string PromoCode { get; set; }
		public bool PromoApplied { get; set; }
		public string PromoProblem { get; set; }
		public bool FreeShipping { get; set; }
		/// <summary>
		/// Whether an order can be placed from this quote: every line can be fulfilled and a promo code the customer
		/// entered applies. A code that does not apply blocks the order, otherwise the customer would pay more than they expect.
		/// </summary>
		public bool IsValid => Lines.Count > 0 && Lines.All(l => l.Problem == null) && (PromoCode == null || PromoApplied);
	}

	public class OrderCreatedResponse
	{
		public Guid OrderId { get; set; }
		public string OrderNumber { get; set; }
		public decimal Total { get; set; }
		public string Currency { get; set; }
		public string CheckoutUrl { get; set; }
		public string PaymentToken { get; set; }
		public DateTimeOffset ExpiresOn { get; set; }
	}

	public class OrderStatusView
	{
		public Guid Id { get; set; }
		public string OrderNumber { get; set; }
		public OrderStatus Status { get; set; }
		public PaymentStatus? PaymentStatus { get; set; }
		public List<OrderItem> Items { get; set; } = new();
		public decimal Subtotal { get; set; }
		public decimal DiscountTotal { get; set; }
		public decimal ShippingCost { get; set; }
		public decimal Total { get; set; }
		public string Currency { get; set; }
		public string PromoCode { get; set; }
		public string Email { get; set; }
		public string FirstName { get; set; }
		public string LastName { get; set; }
		public string PhoneNumber { get; set; }
		public string City { get; set; }
		public string StreetAddress { get; set; }
		public string PostalCode { get; set; }
		public string Country { get; set; }
		public DeliveryMethod DeliveryMethod { get; set; }
		public PaymentMethod PaymentMethod { get; set; }
		public DateTimeOffset OrderDate { get; set; }
		public DateTimeOffset? PaidOn { get; set; }
		public DateTimeOffset? ShippedOn { get; set; }
		public DateTimeOffset ExpiresOn { get; set; }
		public string TrackingNumber { get; set; }
		/// <summary>Present while the order can still be paid through the existing checkout.</summary>
		public string CheckoutUrl { get; set; }
		public bool CanRetryPayment { get; set; }

		public static OrderStatusView FromOrder(ShopOrder order, DateTimeOffset now)
		{
			Payment latest = order.Payments?.OrderByDescending(p => p.CreatedOn).FirstOrDefault();
			bool awaiting = order.Status == OrderStatus.AwaitingPayment && order.ExpiresOn > now;
			bool pendingPayment = latest != null
				&& (latest.Status == Models.PaymentStatus.Pending || latest.Status == Models.PaymentStatus.Authorised);
			return new OrderStatusView
			{
				Id = order.Id,
				OrderNumber = order.OrderNumber,
				Status = order.Status,
				PaymentStatus = latest?.Status,
				Items = order.Items?.OrderBy(i => i.Id).ToList() ?? new List<OrderItem>(),
				Subtotal = order.Subtotal,
				DiscountTotal = order.DiscountTotal,
				ShippingCost = order.ShippingCost,
				Total = order.Total,
				Currency = order.Currency,
				PromoCode = order.PromoCode,
				Email = order.Email,
				FirstName = order.FirstName,
				LastName = order.LastName,
				PhoneNumber = order.PhoneNumber,
				City = order.City,
				StreetAddress = order.StreetAddress,
				PostalCode = order.PostalCode,
				Country = order.Country,
				DeliveryMethod = order.DeliveryMethod,
				PaymentMethod = order.PaymentMethod,
				OrderDate = order.OrderDate,
				PaidOn = order.PaidOn,
				ShippedOn = order.ShippedOn,
				ExpiresOn = order.ExpiresOn,
				TrackingNumber = order.TrackingNumber,
				CheckoutUrl = awaiting && pendingPayment ? latest.CheckoutUrl : null,
				CanRetryPayment = awaiting && (latest == null || !pendingPayment)
			};
		}
	}

	public class OrderStatusUpdate
	{
		[Required]
		public OrderStatus Status { get; set; }

		public string TrackingNumber { get; set; }

		[MaxLength(1000)]
		public string Note { get; set; }
	}

	public class ImageUploadRequest
	{
		[Required]
		public string Image { get; set; }
	}

	public class WebhookRegistrationRequest
	{
		[Required]
		[Url]
		public string Url { get; set; }
	}
}
