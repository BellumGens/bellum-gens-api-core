using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace BellumGens.Api.Core.Models
{
	/// <summary>A delivery address a signed-in customer saved for later orders. The shop delivers within Bulgaria only.</summary>
	public class ShippingAddress
	{
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public Guid Id { get; set; }

		[Required]
		[MaxLength(450)]
		[JsonIgnore]
		public string UserId { get; set; }

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

		/// <summary>Several Bulgarian towns share a name; the postal code tells the courier which one.</summary>
		[Required]
		[MaxLength(10)]
		public string PostalCode { get; set; }

		[Required]
		[MaxLength(2)]
		public string Country { get; set; } = ShopOptions.DeliveryCountry;

		public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.Now;

		/// <summary>When the address was last saved or used for an order; the most recent one is offered first.</summary>
		public DateTimeOffset LastUsedOn { get; set; } = DateTimeOffset.Now;
	}
}
