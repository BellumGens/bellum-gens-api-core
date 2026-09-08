using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace BellumGens.Api.Core.Models
{
    public class ProductVariant
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        public Guid ProductId { get; set; }

        /// <summary>Display name, e.g. "Male / L". Snapshotted onto order lines.</summary>
        [Required]
        public string Name { get; set; }

        public JerseyCut? Cut { get; set; }

        public JerseySize? Size { get; set; }

        public string Sku { get; set; }

        /// <summary>Overrides the product price for this variant when set.</summary>
        public decimal? PriceOverride { get; set; }

        /// <summary>Null means stock is not tracked for this variant.</summary>
        public int? StockQuantity { get; set; }

        public bool Active { get; set; } = true;

        public int SortOrder { get; set; }

        [Timestamp]
        [JsonIgnore]
        public byte[] RowVersion { get; set; }

        [JsonIgnore]
        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }
    }
}
