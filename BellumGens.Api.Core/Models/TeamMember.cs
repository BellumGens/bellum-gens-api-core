﻿using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace BellumGens.Api.Core.Models
{
	public class TeamMember
	{
		public Guid TeamId { get; set; }

		public string UserId { get; set; }

		public bool IsActive { get; set; }

		public bool IsAdmin { get; set; }

		public bool IsEditor { get; set; }

		public PlaystyleRole Role { get; set; }

        [NotMapped]
        public string Username
        { 
            get
            {
                return Member?.UserName;
            }
        }

        [NotMapped]
        public string AvatarIcon
        {
            get
            {
                return Member?.CSGODetails?.AvatarIcon;
            }
        }

        [NotMapped]
        public string AvatarMedium
        {
            get
            {
                return Member?.CSGODetails?.AvatarMedium;
            }
        }

        [NotMapped]
        public string CustomUrl
        {
            get
            {
                return Member?.CSGODetails?.CustomUrl;
            }
        }

        [NotMapped]
        public string AvatarFull
        {
            get
            {
                return Member?.CSGODetails?.AvatarFull;
            }
        }

        [NotMapped]
        public string Country
        {
            get
            {
                return Member?.CSGODetails?.Country;
            }
        }

        [NotMapped]
        public string RealName
        {
            get
            {
                return Member?.CSGODetails?.RealName;
            }
        }

        [NotMapped]
        public string SteamId
        {
            get
            {
                return Member?.SteamID;
            }
        }

        [ForeignKey("TeamId")]
        [JsonIgnore]
		public CSGOTeam Team { get; set; } 

		[ForeignKey("UserId")]
        [JsonIgnore]
		public virtual ApplicationUser Member { get; set; }
	}
}