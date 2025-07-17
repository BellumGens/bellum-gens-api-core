using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace BellumGens.Api.Core.Models
{
    public class OrderDetails
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public Guid OrderId { get; set; }
        public JerseyCut Cut { get; set; }
        public JerseySize Size { get; set; }

        [JsonIgnore]
        [ForeignKey("OrderId")]
        public virtual Order Order { get; set; }
    }
}