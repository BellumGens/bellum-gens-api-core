using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace BellumGens.Api.Core.Models
{
	public class Tournament
	{
		private const string InviteCodeCharacters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

		public const int InviteCodeLength = 8;

		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public Guid ID { get; set; }

		public string Name { get; set; }

		public string Description { get; set; }

		public string Logo { get; set; }

		public DateTimeOffset? StartDate { get; set; }

		public DateTimeOffset? EndDate { get; set; }

		public bool Active { get; set; } = false;

		/// <summary>The game the tournament is played in; null for multi-game leagues.</summary>
		public Game? Game { get; set; }

		public TournamentVisibility Visibility { get; set; } = TournamentVisibility.Public;

		public TournamentStatus Status { get; set; } = TournamentStatus.Draft;

		/// <summary>The id of the user who created (owns) the tournament; null for tournaments created before ownership existed.</summary>
		public string CreatorId { get; set; }

		/// <summary>The code participants join with. Only the owner and admins should ever receive it.</summary>
		public string InviteCode { get; set; }

		public int? MaxParticipants { get; set; }

		public string PrizePool { get; set; }

		[JsonIgnore]
		public virtual ICollection<TournamentCSGOMatch> CSGOMatches { get; set; } = new HashSet<TournamentCSGOMatch>();

		[JsonIgnore]
		public virtual ICollection<TournamentSC2Match> SC2Matches { get; set; } = new HashSet<TournamentSC2Match>();

		[JsonIgnore]
		public virtual ICollection<TournamentCSGOGroup> CSGOGroups { get; set; } = new HashSet<TournamentCSGOGroup>();

		[JsonIgnore]
		public virtual ICollection<TournamentSC2Group> SC2Groups { get; set; } = new HashSet<TournamentSC2Group>();

		[JsonIgnore]
		public virtual ICollection<TournamentApplication> Applications { get; set; } = new HashSet<TournamentApplication>();

		/// <summary>Whether participants can still join: the tournament hasn't started, finished or been cancelled.</summary>
		[JsonIgnore]
		[NotMapped]
		public bool AcceptsParticipants => Status == TournamentStatus.Draft || Status == TournamentStatus.Open;

		public async Task UniqueInviteCode(BellumGensDbContext context)
		{
			if (string.IsNullOrEmpty(InviteCode))
			{
				InviteCode = GenerateInviteCode();
				while (await context.Tournaments.AnyAsync(t => t.InviteCode == InviteCode && t.ID != ID))
				{
					InviteCode = GenerateInviteCode();
				}
			}
		}

		public static string NormalizeInviteCode(string code)
		{
			return code?.Trim().ToUpperInvariant();
		}

		private static string GenerateInviteCode()
		{
			return new string(Enumerable.Range(0, InviteCodeLength)
				.Select(_ => InviteCodeCharacters[RandomNumberGenerator.GetInt32(InviteCodeCharacters.Length)])
				.ToArray());
		}
	}
}