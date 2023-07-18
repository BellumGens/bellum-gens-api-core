using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace BellumGens.Api.Core.Models
{
	public class UserMapPool
	{
		public string UserId { get; set; }

		[JsonPropertyName("mapId")]
		public CSGOMap Map { get; set; }
		public bool IsPlayed { get; set; }

		[JsonIgnore]
		[ForeignKey("UserId")]
		public virtual ApplicationUser User { get; set; }
	}
}