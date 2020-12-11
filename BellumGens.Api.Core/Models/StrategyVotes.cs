using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace BellumGens.Api.Core.Models
{
	public class StrategyVote
	{
		public Guid StratId { get; set; }

		public string UserId { get; set; }

		public VoteDirection Vote { get; set; }

		[JsonIgnore]
		[ForeignKey("StratId")]
		public virtual CSGOStrategy Strategy { get; set; }

		[JsonIgnore]
		[ForeignKey("UserId")]
		public virtual ApplicationUser User { get; set; }
	}
}