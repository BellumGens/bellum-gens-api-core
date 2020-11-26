using System;

namespace BellumGens.Api.Core.Models
{
	public class VoteModel
	{
		public Guid id { get; set; }
		public VoteDirection direction { get; set; }
	}
}
