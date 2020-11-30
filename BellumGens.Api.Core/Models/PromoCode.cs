using System;
using System.ComponentModel.DataAnnotations;

namespace BellumGens.Api.Core.Models
{
    public class Promo
    {
        [Key]
        public string Code { get; set; }
        public decimal Discount { get; set; }
        public DateTimeOffset? Expiration { get; set; }
    }
}