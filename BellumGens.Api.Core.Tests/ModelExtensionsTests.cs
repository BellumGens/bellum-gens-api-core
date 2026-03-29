using System;
using System.Collections.Generic;
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
    }
}
