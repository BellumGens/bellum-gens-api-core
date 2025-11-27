using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BellumGens.Api.Core.Models
{
    public class EarlyBird
    {
        [Key]
        public string UserId { get; set; }

        [EmailAddress]
        public string Email { get; set; }
        public bool FirstTime { get; set; } = true;
        public DateTimeOffset RegisteredOn { get; set; } = DateTimeOffset.Now;

        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }
    }
}
