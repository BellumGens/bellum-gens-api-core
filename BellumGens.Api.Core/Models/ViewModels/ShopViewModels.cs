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

    public class CreateOrderRequest : QuoteRequest
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

        [Required]
        public string City { get; set; }

        [Required]
        public string StreetAddress { get; set; }

        public string PostalCode { get; set; }

        public string Country { get; set; } = "BG";

        public string Language { get; set; } = "bg";

        [MaxLength(1000)]
        public string CustomerNote { get; set; }

        [Range(typeof(bool), "true", "true", ErrorMessage = "The terms and conditions must be accepted.")]
        public bool AcceptedTerms { get; set; }
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
        public bool IsValid => Lines.Count > 0 && Lines.All(l => l.Problem == null);
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
