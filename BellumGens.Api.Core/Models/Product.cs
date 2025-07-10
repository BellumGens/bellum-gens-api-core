using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace BellumGens.Api.Core.Models
{
    public class Product
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }
        public ProductType ProductType { get; set; }
        public int Quantity { get; set; }
        [Precision(6, 2)]
        public decimal Price { get; set; }
        [Precision(3, 2)]
        public decimal? Discount { get; set; }

    }
}
