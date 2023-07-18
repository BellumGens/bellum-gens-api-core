using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BellumGens.Api.Core.Models
{
	public class Application
	{
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public Guid Id { get; set; }

		public string Message { get; set; }

		public NotificationState State { get; set; }

		[DatabaseGenerated(DatabaseGeneratedOption.Computed)]
		public DateTimeOffset? Sent { get; set; } = DateTimeOffset.Now;
	}
}