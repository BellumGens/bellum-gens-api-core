using BellumGens.Api.Core.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.Json.Serialization;

namespace BellumGens.Api.Core.Models
{
	public class CSGOTeam
	{
		public void UniqueCustomUrl(BellumGensDbContext context)
		{
			if (string.IsNullOrEmpty(CustomUrl))
			{
				var parts = TeamName.Split(' ');
				string url = string.Join("-", parts);
				while (context.CSGOTeams.Where(t => t.CustomUrl == url).SingleOrDefault() != null)
				{
					if (url.Length > 58)
						url = url[..58];
					url += '-' + Util.GenerateHashString(6);
				}
				CustomUrl = url;
			}
		}

		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public Guid TeamId { get; set; }

        [MaxLength(64)]
        public string SteamGroupId { get; set; }

		public string TeamName { get; set; }

		public string TeamAvatar { get; set; }

		public string Description { get; set; }

		public string Discord { get; set; }

		public DateTimeOffset RegisteredOn { get; set; } = DateTimeOffset.Now;

		public bool Visible { get; set; } = true;

		[MaxLength(64)]
		public string CustomUrl { get; set; }

		[JsonIgnore]
		public virtual ICollection<CSGOStrategy> Strategies { get; set; } = new HashSet<CSGOStrategy>();

		public virtual ICollection<TeamAvailability> PracticeSchedule { get; set; }

		public virtual ICollection<TeamMember> Members { get; set; } = new HashSet<TeamMember>();

		[JsonIgnore]
		public virtual ICollection<TeamInvite> Invites { get; set; } = new HashSet<TeamInvite>();

		[JsonIgnore]
		public virtual ICollection<TeamApplication> Applications { get; set; } = new HashSet<TeamApplication>();

		[JsonIgnore]
		public virtual ICollection<TeamMapPool> MapPool { get; set; }

		//[NotMapped]
		//public virtual SteamGroup SteamGroup
		//{
		//	get
		//	{
		//		if (SteamGroupId != null && _steamGroup == null)
		//		{
		//			_steamGroup = _steamService.GetSteamGroup(SteamGroupId).Result;
		//		}
		//		return _steamGroup;
		//	}
		//}
	}
}