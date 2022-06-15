using System;

namespace BellumGens.Api.Core.Models
{
	public class TeamSearchModel
	{
		public PlaystyleRole? Role { get; set; }
		public double Overlap { get; set; }
	}

	public class PlayerSearchModel
	{
		public PlaystyleRole? Role { get; set; }
		public double Overlap { get; set; }
		public Guid? TeamId { get; set; }
	}
}