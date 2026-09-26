using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using BellumGens.Api.Core.Models;
using BellumGens.Api.Core.Models.Extensions;

namespace BellumGens.Api.Core.Tests
{
    public class ModelExtensionsTests
    {
        [Fact]
        public void GetUserId_WithNameIdentifierClaim_ReturnsUserId()
        {
            var userId = "https://steamcommunity.com/openid/id/12345678901234567";
            var principal = TestUtils.CreateAuthenticatedUser(userId);

            var result = principal.GetUserId();

            Assert.Equal(userId, result);
        }

        [Fact]
        public void GetUserId_WithNullPrincipal_ThrowsArgumentNullException()
        {
            ClaimsPrincipal principal = null!;

            Assert.Throws<ArgumentNullException>(() => principal.GetUserId());
        }

        [Fact]
        public void GetUserId_WithNoClaims_ReturnsNull()
        {
            var principal = TestUtils.CreateUnauthenticatedUser();

            var result = principal.GetUserId();

            Assert.Null(result);
        }

        [Fact]
        public void GetSteamUserId_WithValidSteamUrl_ReturnsSteamId()
        {
            var steamId = "12345678901234567";
            var userId = $"https://steamcommunity.com/openid/id/{steamId}";
            var principal = TestUtils.CreateAuthenticatedUser(userId);

            var result = principal.GetSteamUserId();

            Assert.Equal(steamId, result);
        }

        [Fact]
        public void GetSteamUserId_WithShortPath_ReturnsNull()
        {
            var userId = "simple-user-id";
            var principal = TestUtils.CreateAuthenticatedUser(userId);

            var result = principal.GetSteamUserId();

            Assert.Null(result);
        }

        [Fact]
        public void GetResolvedUserId_WithSteamUrl_ReturnsSteamId()
        {
            var steamId = "12345678901234567";
            var userId = $"https://steamcommunity.com/openid/id/{steamId}";
            var principal = TestUtils.CreateAuthenticatedUser(userId);

            var result = principal.GetResolvedUserId();

            Assert.Equal(steamId, result);
        }

        [Fact]
        public void GetResolvedUserId_WithNonSteamId_FallsBackToFullUserId()
        {
            var userId = "non-steam-user-id";
            var principal = TestUtils.CreateAuthenticatedUser(userId);

            var result = principal.GetResolvedUserId();

            Assert.Equal(userId, result);
        }

        [Fact]
        public void GetTotalAvailability_UserAvailabilityList_ReturnsCorrectTotal()
        {
            var availabilities = new List<UserAvailability>
            {
                new UserAvailability
                {
                    Available = true,
                    Day = DayOfWeek.Monday,
                    From = new DateTimeOffset(2018, 1, 15, 10, 0, 0, TimeSpan.Zero),
                    To = new DateTimeOffset(2018, 1, 15, 14, 0, 0, TimeSpan.Zero)
                },
                new UserAvailability
                {
                    Available = true,
                    Day = DayOfWeek.Tuesday,
                    From = new DateTimeOffset(2018, 1, 15, 8, 0, 0, TimeSpan.Zero),
                    To = new DateTimeOffset(2018, 1, 15, 10, 0, 0, TimeSpan.Zero)
                }
            };

            var result = availabilities.GetTotalAvailability();

            Assert.Equal(6.0, result);
        }

        [Fact]
        public void GetTotalAvailability_EmptyUserAvailabilityList_ReturnsZero()
        {
            var availabilities = new List<UserAvailability>();

            var result = availabilities.GetTotalAvailability();

            Assert.Equal(0.0, result);
        }

        [Fact]
        public void GetTotalAvailability_TeamAvailabilityList_ReturnsCorrectTotal()
        {
            var availabilities = new List<TeamAvailability>
            {
                new TeamAvailability
                {
                    Available = true,
                    Day = DayOfWeek.Wednesday,
                    From = new DateTimeOffset(2018, 1, 15, 18, 0, 0, TimeSpan.Zero),
                    To = new DateTimeOffset(2018, 1, 15, 21, 0, 0, TimeSpan.Zero)
                }
            };

            var result = availabilities.GetTotalAvailability();

            Assert.Equal(3.0, result);
        }

        [Fact]
        public void GetTotalAvailability_EmptyTeamAvailabilityList_ReturnsZero()
        {
            var availabilities = new List<TeamAvailability>();

            var result = availabilities.GetTotalAvailability();

            Assert.Equal(0.0, result);
        }

        [Fact]
        public void GetTotalAvailability_UserAvailabilityList_IgnoresUnavailableDays()
        {
            var availabilities = new List<UserAvailability>
            {
                UserSlot(DayOfWeek.Monday, 18, 22),
                UserSlot(DayOfWeek.Tuesday, 10, 20, available: false)
            };

            var result = availabilities.GetTotalAvailability();

            Assert.Equal(4.0, result);
        }

        [Fact]
        public void GetTotalAvailability_TeamAvailabilityList_IgnoresUnavailableDays()
        {
            var availabilities = new List<TeamAvailability>
            {
                TeamSlot(DayOfWeek.Monday, 18, 21),
                TeamSlot(DayOfWeek.Tuesday, 10, 20, available: false)
            };

            var result = availabilities.GetTotalAvailability();

            Assert.Equal(3.0, result);
        }

        [Fact]
        public void GetTotalAvailability_ListOverloads_MatchEntityOverloads()
        {
            var userSlots = new List<UserAvailability>
            {
                UserSlot(DayOfWeek.Monday, 18, 22),
                UserSlot(DayOfWeek.Friday, 12, 20, available: false)
            };
            var teamSlots = new List<TeamAvailability>
            {
                TeamSlot(DayOfWeek.Monday, 19, 23),
                TeamSlot(DayOfWeek.Friday, 12, 20, available: false)
            };
            var user = new ApplicationUser { Availability = userSlots };
            var team = new CSGOTeam { PracticeSchedule = teamSlots };

            Assert.Equal(user.GetTotalAvailability(), userSlots.GetTotalAvailability());
            Assert.Equal(team.GetTotalAvailability(), teamSlots.GetTotalAvailability());
        }

        // (other window from, to, user window from, to, expected hours), all on the same day.
        // The "other" window is the team practice schedule / the other player's availability.
        public static TheoryData<int, int, int, int, double> OverlapCases => new()
        {
            // Other window starts later and ends later than the user's: overlap is 19-22
            { 19, 23, 18, 22, 3.0 },
            // Mirrored: other window starts earlier and ends earlier: overlap is 19-22
            { 18, 22, 19, 23, 3.0 },
            // Other window contains the user's window
            { 17, 23, 18, 22, 4.0 },
            // User window contains the other window
            { 19, 21, 18, 22, 2.0 },
            // Identical windows
            { 18, 22, 18, 22, 4.0 },
            // Touching edges: other ends exactly when the user starts
            { 14, 18, 18, 22, 0.0 },
            // Touching edges: other starts exactly when the user ends
            { 22, 23, 18, 22, 0.0 },
            // Disjoint windows
            { 8, 12, 18, 22, 0.0 }
        };

        [Theory]
        [MemberData(nameof(OverlapCases))]
        public void GetTotalOverlap_Team_ReturnsIntervalIntersection(int otherFrom, int otherTo, int userFrom, int userTo, double expected)
        {
            var team = new CSGOTeam { PracticeSchedule = [TeamSlot(DayOfWeek.Monday, otherFrom, otherTo)] };
            var user = new ApplicationUser { Availability = [UserSlot(DayOfWeek.Monday, userFrom, userTo)] };

            Assert.Equal(expected, team.GetTotalOverlap(user));
        }

        [Theory]
        [MemberData(nameof(OverlapCases))]
        public void GetTotalOverlap_TeamAvailabilityList_ReturnsIntervalIntersection(int otherFrom, int otherTo, int userFrom, int userTo, double expected)
        {
            var schedule = new List<TeamAvailability> { TeamSlot(DayOfWeek.Monday, otherFrom, otherTo) };
            var user = new ApplicationUser { Availability = [UserSlot(DayOfWeek.Monday, userFrom, userTo)] };

            Assert.Equal(expected, schedule.GetTotalOverlap(user));
        }

        [Theory]
        [MemberData(nameof(OverlapCases))]
        public void GetTotalOverlap_Player_ReturnsIntervalIntersection(int otherFrom, int otherTo, int userFrom, int userTo, double expected)
        {
            var player = new ApplicationUser { Availability = [UserSlot(DayOfWeek.Monday, otherFrom, otherTo)] };
            var user = new ApplicationUser { Availability = [UserSlot(DayOfWeek.Monday, userFrom, userTo)] };

            Assert.Equal(expected, player.GetTotalOverlap(user));
            // Overlap between two players is symmetric
            Assert.Equal(expected, user.GetTotalOverlap(player));
        }

        [Fact]
        public void GetTotalOverlap_DifferentDays_ReturnsZero()
        {
            var schedule = new List<TeamAvailability> { TeamSlot(DayOfWeek.Tuesday, 18, 22) };
            var team = new CSGOTeam { PracticeSchedule = schedule };
            var player = new ApplicationUser { Availability = [UserSlot(DayOfWeek.Tuesday, 18, 22)] };
            var user = new ApplicationUser { Availability = [UserSlot(DayOfWeek.Monday, 18, 22)] };

            Assert.Equal(0.0, team.GetTotalOverlap(user));
            Assert.Equal(0.0, schedule.GetTotalOverlap(user));
            Assert.Equal(0.0, player.GetTotalOverlap(user));
        }

        [Fact]
        public void GetTotalOverlap_SumsAcrossMatchingDays_AndIgnoresUnavailableSlots()
        {
            var schedule = new List<TeamAvailability>
            {
                TeamSlot(DayOfWeek.Monday, 19, 23),                    // 3h with the user's 18-22
                TeamSlot(DayOfWeek.Wednesday, 16, 20),                 // 2h with the user's 18-22
                TeamSlot(DayOfWeek.Friday, 18, 22, available: false),  // team not available
                TeamSlot(DayOfWeek.Saturday, 18, 22)                   // user not available
            };
            var user = new ApplicationUser
            {
                Availability =
                [
                    UserSlot(DayOfWeek.Monday, 18, 22),
                    UserSlot(DayOfWeek.Wednesday, 18, 22),
                    UserSlot(DayOfWeek.Friday, 18, 22),
                    UserSlot(DayOfWeek.Saturday, 18, 22, available: false)
                ]
            };
            var team = new CSGOTeam { PracticeSchedule = schedule };
            var player = new ApplicationUser
            {
                Availability = schedule.Select(s => UserSlot(s.Day, s.From.Hour, s.To.Hour, s.Available)).ToList()
            };

            Assert.Equal(5.0, schedule.GetTotalOverlap(user));
            Assert.Equal(5.0, team.GetTotalOverlap(user));
            Assert.Equal(5.0, player.GetTotalOverlap(user));
        }

        private static DateTimeOffset At(int hour)
        {
            return new DateTimeOffset(2018, 1, 15, hour, 0, 0, TimeSpan.Zero);
        }

        private static UserAvailability UserSlot(DayOfWeek day, int from, int to, bool available = true)
        {
            return new UserAvailability { Day = day, From = At(from), To = At(to), Available = available };
        }

        private static TeamAvailability TeamSlot(DayOfWeek day, int from, int to, bool available = true)
        {
            return new TeamAvailability { Day = day, From = At(from), To = At(to), Available = available };
        }
    }
}
