﻿using BellumGens.Api.Core.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.Json.Serialization;

namespace BellumGens.Api.Core.Models
{
	public class CSGOStrategy
	{
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public Guid Id { get; set; }

		public Guid? TeamId { get; set; }

		public string UserId { get; set; }

		public Side Side { get; set; }

		public string Title { get; set; }

		public CSGOMap Map { get; set; }

		public string Description { get; set; }

		public string Url { get; set; }

		[Column("Image")]
		public string StratImage { get; set; }

		public string EditorMetadata { get; set; }

		public bool Visible { get; set; }

		public string PrivateShareLink { get; set; }

		public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.Now;

		[MaxLength(64)]
		public string CustomUrl { get; set; }

		[NotMapped]
		public int Rating
		{
			get
			{
				return Votes == null ? 0 : Votes.Where(v => v.Vote == VoteDirection.Up).Count() - Votes.Where(v => v.Vote == VoteDirection.Down).Count();
			}
			private set { }
		}

		[NotMapped]
		public string Owner
		{
			get
			{
				return User?.UserName;
			}
		}

		public virtual ICollection<StrategyVote> Votes { get; set; }

		public virtual ICollection<StrategyComment> Comments { get; set; }

		[JsonIgnore]
		[ForeignKey("TeamId")]
		public virtual CSGOTeam Team { get; set; }

		[JsonIgnore]
		[ForeignKey("UserId")]
		public virtual ApplicationUser User { get; set; }

		public void UniqueCustomUrl(BellumGensDbContext context)
		{
			if (string.IsNullOrEmpty(CustomUrl))
			{
				var parts = Title.Split(' ');
				string url = string.Join("-", parts);
				while (context.CSGOStrategies.Where(s => s.CustomUrl == url).SingleOrDefault() != null)
				{
					if (url.Length > 58)
						url = url.Substring(0, 58);
					url += '-' + Util.GenerateHashString(6);
				}
				CustomUrl = url;
			}
		}
	}
}