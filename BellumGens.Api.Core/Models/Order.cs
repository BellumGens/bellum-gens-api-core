using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BellumGens.Api.Core.Models
{
    public class Order
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        [EmailAddress]
        [Required]
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
        public string PromoCode { get; set; }
        public bool Confirmed { get; set; } = false;
        public bool Shipped { get; set; } = false;
        public bool Paid { get; set; } = false;
        public DateTimeOffset OrderDate { get; set; } = DateTimeOffset.Now;
        public DateTimeOffset? ShippedDate { get; set; } = null;
        public virtual Promo Promo { get; set; }
        public decimal TotalPrice { get; set; } = 0;
        public PaymentType PaymentType { get; set; } = PaymentType.CashOnDelivery;
        public virtual ICollection<OrderDetails> Jerseys { get; set; } = new HashSet<OrderDetails>();
    }
}