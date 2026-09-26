using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace BellumGens.Api.Core.Models.Extensions
{
	public static class ModelExtensions
	{
		public static string GetUserId(this ClaimsPrincipal principal)
		{
			if (principal == null)
				throw new ArgumentNullException(nameof(principal));

			return principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
		}

		public static string GetSteamUserId(this ClaimsPrincipal identity)
		{
			var parts = identity.GetUserId().Split('/');
			return parts.Length >= 6 ? parts[5] : null;
		}

		public static string GetResolvedUserId(this ClaimsPrincipal identity)
		{
			string userId = identity.GetSteamUserId();
			if (userId == null)
				userId = identity.GetUserId();
			return userId;
		}

		public static double GetTotalAvailability(this ApplicationUser user)
		{
			double total = 0;
			foreach (Availability availability in user.Availability.Where(a => a.Available))
			{
				total += (availability.To - availability.From).TotalHours;
			}
			return total;
		}

		public static double GetTotalAvailability(this CSGOTeam team)
		{
			double total = 0;
			foreach (Availability availability in team.PracticeSchedule.Where(a => a.Available))
			{
				total += (availability.To - availability.From).TotalHours;
			}
			return total;
		}

		public static double GetTotalAvailability(this List<UserAvailability> availabilities)
		{
			double total = 0;
			foreach (Availability availability in availabilities.Where(a => a.Available))
			{
				total += (availability.To - availability.From).TotalHours;
			}
			return total;
		}

		public static double GetTotalAvailability(this List<TeamAvailability> availabilities)
		{
			double total = 0;
			foreach (Availability availability in availabilities.Where(a => a.Available))
			{
				total += (availability.To - availability.From).TotalHours;
			}
			return total;
		}

		public static double GetTotalOverlap(this CSGOTeam team, ApplicationUser user)
		{
			return SumOverlap(team.PracticeSchedule, user.Availability);
		}

		public static double GetTotalOverlap(this List<TeamAvailability> practiseSchedule, ApplicationUser user)
		{
			return SumOverlap(practiseSchedule, user.Availability);
		}

		public static double GetTotalOverlap(this ApplicationUser player, ApplicationUser user)
		{
			return SumOverlap(player.Availability, user.Availability);
		}

		/// <summary>
		/// Sums, in hours, the intersection of each available window in <paramref name="schedule"/>
		/// with the available window for the same day in <paramref name="availabilities"/>.
		/// </summary>
		private static double SumOverlap(IEnumerable<Availability> schedule, IEnumerable<UserAvailability> availabilities)
		{
			double total = 0;
			foreach (Availability practice in schedule.Where(d => d.Available))
			{
				UserAvailability availability = availabilities.SingleOrDefault(a => a.Day == practice.Day && a.Available);
				if (availability != null)
				{
					total += GetOverlap(practice, availability);
				}
			}
			return total;
		}

		/// <summary>
		/// Standard interval intersection: max(0, min(endA, endB) - max(startA, startB)), in hours.
		/// </summary>
		private static double GetOverlap(Availability first, Availability second)
		{
			DateTimeOffset start = first.From > second.From ? first.From : second.From;
			DateTimeOffset end = first.To < second.To ? first.To : second.To;
			return Math.Max(0, (end - start).TotalHours);
		}
	}
}