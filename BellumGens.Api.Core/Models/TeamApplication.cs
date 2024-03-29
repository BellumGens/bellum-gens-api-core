﻿using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace BellumGens.Api.Core.Models
{
	public class TeamApplication : Application
	{
		public string ApplicantId { get; set; }

		public Guid TeamId { get; set; }

        [NotMapped]
        public string UserName
        {
            get
            {
                return User?.UserName;
            }
        }

        [NotMapped]
        public string AvatarIcon
        {
            get
            {
                return User?.CSGODetails?.AvatarIcon;
            }
        }

        [ForeignKey("ApplicantId")]
		[JsonIgnore]
		public virtual ApplicationUser User { get; set; }

		[ForeignKey("TeamId")]
		[JsonIgnore]
		public virtual CSGOTeam Team { get; set; }
	}
}