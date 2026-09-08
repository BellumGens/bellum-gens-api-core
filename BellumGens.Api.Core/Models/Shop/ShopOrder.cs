using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BellumGens.Api.Core.Models
{
    public class ShopOrder
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        /// <summary>Database-generated running number used to build the human readable order number.</summary>
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int OrderSequence { get; set; }

        [NotMapped]
        public string OrderNumber => $"BG-{OrderDate.Year}-{OrderSequence:D6}";

        /// <summary>Set when the customer was logged in at checkout.</summary>
        public string UserId { get; set; }

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

        /// <summary>Storefront language at checkout, drives the email language.</summary>
        public string Language { get; set; } = "bg";

        public string PromoCode { get; set; }

        public DeliveryMethod DeliveryMethod { get; set; } = DeliveryMethod.Courier;

        public decimal Subtotal { get; set; }

        public decimal DiscountTotal { get; set; }

        public decimal ShippingCost { get; set; }

        public decimal Total { get; set; }

        public string Currency { get; set; } = "EUR";

        public OrderStatus Status { get; set; } = OrderStatus.AwaitingPayment;

        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Revolut;

        public string CustomerNote { get; set; }

        public string AdminNote { get; set; }

        public string TrackingNumber { get; set; }

        public DateTimeOffset OrderDate { get; set; } = DateTimeOffset.Now;

        public DateTimeOffset? PaidOn { get; set; }

        public DateTimeOffset? ShippedOn { get; set; }

        /// <summary>Unpaid orders past this point are cancelled and their stock released.</summary>
        public DateTimeOffset ExpiresOn { get; set; }

        public virtual Promo Promo { get; set; }

        public virtual ICollection<OrderItem> Items { get; set; } = new HashSet<OrderItem>();

        public virtual ICollection<Payment> Payments { get; set; } = new HashSet<Payment>();
    }
}
