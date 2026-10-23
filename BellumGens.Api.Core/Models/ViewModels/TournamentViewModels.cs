using System;
using System.Text.Json.Serialization;
using BellumGens.Api.Core.Models.Extensions;

namespace BellumGens.Api.Core.Models
{
	/// <summary>
	/// A tournament as the clients submit it for creation or update. Values the client leaves out (null) for
	/// <see cref="Logo"/>, <see cref="Active"/>, <see cref="Visibility"/> and <see cref="Status"/> keep the current
	/// value on update; the server-managed creator and invite code are never taken from the client.
	/// </summary>
	public class TournamentViewModel
	{
		[JsonConverter(typeof(EmptyStringGuidConverter))]
		public Guid ID { get; set; }

		public string Name { get; set; }

		public string Description { get; set; }

		public string Logo { get; set; }

		public DateTimeOffset? StartDate { get; set; }

		public DateTimeOffset? EndDate { get; set; }

		public bool? Active { get; set; }

		public Game? Game { get; set; }

		public TournamentVisibility? Visibility { get; set; }

		public TournamentStatus? Status { get; set; }

		public int? MaxParticipants { get; set; }

		public string PrizePool { get; set; }
	}

	public class TournamentJoinViewModel
	{
		public string InviteCode { get; set; }
	}
}
