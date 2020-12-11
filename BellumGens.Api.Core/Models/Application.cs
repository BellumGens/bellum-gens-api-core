using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace BellumGens.Api.Core.Models
{
	public class Application
	{
		public string Message { get; set; }

		public NotificationState State { get; set; }

		[DatabaseGenerated(DatabaseGeneratedOption.Computed)]
		public DateTimeOffset? Sent { get; set; }
	}
}