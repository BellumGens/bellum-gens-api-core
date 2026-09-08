using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace BellumGens.Api.Core.Models
{
    public class OrderItem
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public Guid OrderId { get; set; }

        public Guid ProductId { get; set; }

        public Guid? VariantId { get; set; }

        /// <summary>Snapshot of the product name at the time of the order.</summary>
        [Required]
        public string ProductName { get; set; }

        public string VariantName { get; set; }

        /// <summary>Unit price actually charged, after the product discount.</summary>
        public decimal UnitPrice { get; set; }

        public int Quantity { get; set; }

        public decimal LineTotal { get; set; }

        [JsonIgnore]
        [ForeignKey("OrderId")]
        public virtual ShopOrder Order { get; set; }

        [JsonIgnore]
        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }

        [JsonIgnore]
        [ForeignKey("VariantId")]
        public virtual ProductVariant Variant { get; set; }
    }
}
