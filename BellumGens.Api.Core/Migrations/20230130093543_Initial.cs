using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BellumGens.Api.Core.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Logo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Website = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "CSGODetails",
                columns: table => new
                {
                    SteamId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AvatarFull = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AvatarMedium = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AvatarIcon = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RealName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Country = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HeadshotPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    KillDeathRatio = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: false),
                    Accuracy = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    SteamPrivate = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CSGODetails", x => x.SteamId);
                });

            migrationBuilder.CreateTable(
                name: "CSGOTeams",
                columns: table => new
                {
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SteamGroupId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TeamName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TeamAvatar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Discord = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RegisteredOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Visible = table.Column<bool>(type: "bit", nullable: false),
                    CustomUrl = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CSGOTeams", x => x.TeamId);
                });

            migrationBuilder.CreateTable(
                name: "PromoCodes",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Discount = table.Column<decimal>(type: "decimal(3,2)", precision: 3, scale: 2, nullable: false),
                    Expiration = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromoCodes", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "StarCraft2Details",
                columns: table => new
                {
                    BattleNetId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BattleNetBattleTag = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StarCraft2Details", x => x.BattleNetId);
                });

            migrationBuilder.CreateTable(
                name: "Subscribers",
                columns: table => new
                {
                    Email = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Subscribed = table.Column<bool>(type: "bit", nullable: false),
                    SubKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscribers", x => x.Email);
                });

            migrationBuilder.CreateTable(
                name: "Tournaments",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Logo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tournaments", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeamAvailabilities",
                columns: table => new
                {
                    Day = table.Column<int>(type: "int", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Available = table.Column<bool>(type: "bit", nullable: false),
                    From = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    To = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamAvailabilities", x => new { x.TeamId, x.Day });
                    table.ForeignKey(
                        name: "FK_TeamAvailabilities_CSGOTeams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "CSGOTeams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeamMapPools",
                columns: table => new
                {
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Map = table.Column<int>(type: "int", nullable: false),
                    IsPlayed = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamMapPools", x => new { x.TeamId, x.Map });
                    table.ForeignKey(
                        name: "FK_TeamMapPools_CSGOTeams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "CSGOTeams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JerseyOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    City = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StreetAddress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PromoCode = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Confirmed = table.Column<bool>(type: "bit", nullable: false),
                    Shipped = table.Column<bool>(type: "bit", nullable: false),
                    OrderDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JerseyOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JerseyOrders_PromoCodes_PromoCode",
                        column: x => x.PromoCode,
                        principalTable: "PromoCodes",
                        principalColumn: "Code");
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ESEA = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SearchVisible = table.Column<bool>(type: "bit", nullable: false),
                    BattleNetId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    TwitchId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SteamID = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    RegisteredOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastSeen = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PreferredPrimaryRole = table.Column<int>(type: "int", nullable: false),
                    PreferredSecondaryRole = table.Column<int>(type: "int", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUsers_CSGODetails_SteamID",
                        column: x => x.SteamID,
                        principalTable: "CSGODetails",
                        principalColumn: "SteamId");
                    table.ForeignKey(
                        name: "FK_AspNetUsers_StarCraft2Details_BattleNetId",
                        column: x => x.BattleNetId,
                        principalTable: "StarCraft2Details",
                        principalColumn: "BattleNetId");
                });

            migrationBuilder.CreateTable(
                name: "TournamentCSGOGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TournamentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentCSGOGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TournamentCSGOGroups_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TournamentSC2Groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TournamentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentSC2Groups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TournamentSC2Groups_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JerseyDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Cut = table.Column<int>(type: "int", nullable: false),
                    Size = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JerseyDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JerseyDetails_JerseyOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "JerseyOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BellumGensPushSubscriptions",
                columns: table => new
                {
                    P256dh = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Auth = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Endpoint = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpirationTime = table.Column<TimeSpan>(type: "time", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BellumGensPushSubscriptions", x => new { x.P256dh, x.Auth });
                    table.ForeignKey(
                        name: "FK_BellumGensPushSubscriptions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CSGOStrategies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Side = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Map = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Url = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Image = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EditorMetadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Visible = table.Column<bool>(type: "bit", nullable: false),
                    PrivateShareLink = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CustomUrl = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CSGOStrategies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CSGOStrategies_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CSGOStrategies_CSGOTeams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "CSGOTeams",
                        principalColumn: "TeamId");
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    From = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    To = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    TimeStamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Messages_AspNetUsers_From",
                        column: x => x.From,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Messages_AspNetUsers_To",
                        column: x => x.To,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TeamApplications",
                columns: table => new
                {
                    ApplicantId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    State = table.Column<int>(type: "int", nullable: false),
                    Sent = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamApplications", x => new { x.ApplicantId, x.TeamId });
                    table.ForeignKey(
                        name: "FK_TeamApplications_AspNetUsers_ApplicantId",
                        column: x => x.ApplicantId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeamApplications_CSGOTeams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "CSGOTeams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeamInvites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvitingUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    InvitedUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    State = table.Column<int>(type: "int", nullable: false),
                    Sent = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamInvites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamInvites_AspNetUsers_InvitedUserId",
                        column: x => x.InvitedUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TeamInvites_AspNetUsers_InvitingUserId",
                        column: x => x.InvitingUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TeamInvites_CSGOTeams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "CSGOTeams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeamMembers",
                columns: table => new
                {
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsAdmin = table.Column<bool>(type: "bit", nullable: false),
                    IsEditor = table.Column<bool>(type: "bit", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamMembers", x => new { x.TeamId, x.UserId });
                    table.ForeignKey(
                        name: "FK_TeamMembers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeamMembers_CSGOTeams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "CSGOTeams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserAvailabilities",
                columns: table => new
                {
                    Day = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Available = table.Column<bool>(type: "bit", nullable: false),
                    From = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    To = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAvailabilities", x => new { x.UserId, x.Day });
                    table.ForeignKey(
                        name: "FK_UserAvailabilities_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserMapPool",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Map = table.Column<int>(type: "int", nullable: false),
                    IsPlayed = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMapPool", x => new { x.UserId, x.Map });
                    table.ForeignKey(
                        name: "FK_UserMapPool_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TournamentCSGOMatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Team1Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Team2Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Team1Points = table.Column<int>(type: "int", nullable: false),
                    Team2Points = table.Column<int>(type: "int", nullable: false),
                    DemoLink = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VideoLink = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NoShow = table.Column<bool>(type: "bit", nullable: false),
                    StartTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentCSGOMatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TournamentCSGOMatches_CSGOTeams_Team1Id",
                        column: x => x.Team1Id,
                        principalTable: "CSGOTeams",
                        principalColumn: "TeamId");
                    table.ForeignKey(
                        name: "FK_TournamentCSGOMatches_CSGOTeams_Team2Id",
                        column: x => x.Team2Id,
                        principalTable: "CSGOTeams",
                        principalColumn: "TeamId");
                    table.ForeignKey(
                        name: "FK_TournamentCSGOMatches_TournamentCSGOGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "TournamentCSGOGroups",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TournamentCSGOMatches_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "TournamentApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompanyId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DateSubmitted = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Game = table.Column<int>(type: "int", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Hash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BattleNetId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    State = table.Column<int>(type: "int", nullable: false),
                    TournamentCSGOGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TournamentSC2GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TournamentApplications_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TournamentApplications_CSGOTeams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "CSGOTeams",
                        principalColumn: "TeamId");
                    table.ForeignKey(
                        name: "FK_TournamentApplications_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Name",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TournamentApplications_TournamentCSGOGroups_TournamentCSGOGroupId",
                        column: x => x.TournamentCSGOGroupId,
                        principalTable: "TournamentCSGOGroups",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TournamentApplications_TournamentSC2Groups_TournamentSC2GroupId",
                        column: x => x.TournamentSC2GroupId,
                        principalTable: "TournamentSC2Groups",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TournamentApplications_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TournamentSC2Matches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Player1Id = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Player2Id = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Player1Points = table.Column<int>(type: "int", nullable: false),
                    Player2Points = table.Column<int>(type: "int", nullable: false),
                    DemoLink = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VideoLink = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NoShow = table.Column<bool>(type: "bit", nullable: false),
                    StartTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentSC2Matches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TournamentSC2Matches_AspNetUsers_Player1Id",
                        column: x => x.Player1Id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TournamentSC2Matches_AspNetUsers_Player2Id",
                        column: x => x.Player2Id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TournamentSC2Matches_TournamentSC2Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "TournamentSC2Groups",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TournamentSC2Matches_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "StrategyComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StratId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Published = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrategyComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StrategyComments_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StrategyComments_CSGOStrategies_StratId",
                        column: x => x.StratId,
                        principalTable: "CSGOStrategies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StrategyVotes",
                columns: table => new
                {
                    StratId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Vote = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrategyVotes", x => new { x.StratId, x.UserId });
                    table.ForeignKey(
                        name: "FK_StrategyVotes_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StrategyVotes_CSGOStrategies_StratId",
                        column: x => x.StratId,
                        principalTable: "CSGOStrategies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CSGOMatchMaps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Map = table.Column<int>(type: "int", nullable: false),
                    CsgoMatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamPickId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TeamBanId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Team1Score = table.Column<int>(type: "int", nullable: false),
                    Team2Score = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CSGOMatchMaps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CSGOMatchMaps_CSGOTeams_TeamBanId",
                        column: x => x.TeamBanId,
                        principalTable: "CSGOTeams",
                        principalColumn: "TeamId");
                    table.ForeignKey(
                        name: "FK_CSGOMatchMaps_CSGOTeams_TeamPickId",
                        column: x => x.TeamPickId,
                        principalTable: "CSGOTeams",
                        principalColumn: "TeamId");
                    table.ForeignKey(
                        name: "FK_CSGOMatchMaps_TournamentCSGOMatches_CsgoMatchId",
                        column: x => x.CsgoMatchId,
                        principalTable: "TournamentCSGOMatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SC2MatchMaps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Map = table.Column<int>(type: "int", nullable: false),
                    Sc2MatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerPickId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    PlayerBanId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    WinnerId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SC2MatchMaps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SC2MatchMaps_AspNetUsers_PlayerBanId",
                        column: x => x.PlayerBanId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SC2MatchMaps_AspNetUsers_PlayerPickId",
                        column: x => x.PlayerPickId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SC2MatchMaps_TournamentSC2Matches_Sc2MatchId",
                        column: x => x.Sc2MatchId,
                        principalTable: "TournamentSC2Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_BattleNetId",
                table: "AspNetUsers",
                column: "BattleNetId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_SteamID",
                table: "AspNetUsers",
                column: "SteamID");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BellumGensPushSubscriptions_UserId",
                table: "BellumGensPushSubscriptions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_Name",
                table: "Companies",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CSGOMatchMaps_CsgoMatchId",
                table: "CSGOMatchMaps",
                column: "CsgoMatchId");

            migrationBuilder.CreateIndex(
                name: "IX_CSGOMatchMaps_TeamBanId",
                table: "CSGOMatchMaps",
                column: "TeamBanId");

            migrationBuilder.CreateIndex(
                name: "IX_CSGOMatchMaps_TeamPickId",
                table: "CSGOMatchMaps",
                column: "TeamPickId");

            migrationBuilder.CreateIndex(
                name: "IX_CSGOStrategies_CustomUrl",
                table: "CSGOStrategies",
                column: "CustomUrl",
                unique: true,
                filter: "[CustomUrl] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CSGOStrategies_TeamId",
                table: "CSGOStrategies",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_CSGOStrategies_UserId",
                table: "CSGOStrategies",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CSGOTeams_CustomUrl",
                table: "CSGOTeams",
                column: "CustomUrl",
                unique: true,
                filter: "[CustomUrl] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_JerseyDetails_OrderId",
                table: "JerseyDetails",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_JerseyOrders_PromoCode",
                table: "JerseyOrders",
                column: "PromoCode");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_From",
                table: "Messages",
                column: "From");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_To",
                table: "Messages",
                column: "To");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodes_Code",
                table: "PromoCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SC2MatchMaps_PlayerBanId",
                table: "SC2MatchMaps",
                column: "PlayerBanId");

            migrationBuilder.CreateIndex(
                name: "IX_SC2MatchMaps_PlayerPickId",
                table: "SC2MatchMaps",
                column: "PlayerPickId");

            migrationBuilder.CreateIndex(
                name: "IX_SC2MatchMaps_Sc2MatchId",
                table: "SC2MatchMaps",
                column: "Sc2MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_StrategyComments_StratId",
                table: "StrategyComments",
                column: "StratId");

            migrationBuilder.CreateIndex(
                name: "IX_StrategyComments_UserId",
                table: "StrategyComments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_StrategyVotes_UserId",
                table: "StrategyVotes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamApplications_TeamId",
                table: "TeamApplications",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamInvites_InvitedUserId",
                table: "TeamInvites",
                column: "InvitedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamInvites_InvitingUserId",
                table: "TeamInvites",
                column: "InvitingUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamInvites_TeamId",
                table: "TeamInvites",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMembers_UserId",
                table: "TeamMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentApplications_CompanyId",
                table: "TournamentApplications",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentApplications_TeamId",
                table: "TournamentApplications",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentApplications_TournamentCSGOGroupId",
                table: "TournamentApplications",
                column: "TournamentCSGOGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentApplications_TournamentId",
                table: "TournamentApplications",
                column: "TournamentId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentApplications_TournamentSC2GroupId",
                table: "TournamentApplications",
                column: "TournamentSC2GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentApplications_UserId",
                table: "TournamentApplications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentCSGOGroups_TournamentId",
                table: "TournamentCSGOGroups",
                column: "TournamentId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentCSGOMatches_GroupId",
                table: "TournamentCSGOMatches",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentCSGOMatches_Team1Id",
                table: "TournamentCSGOMatches",
                column: "Team1Id");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentCSGOMatches_Team2Id",
                table: "TournamentCSGOMatches",
                column: "Team2Id");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentCSGOMatches_TournamentId",
                table: "TournamentCSGOMatches",
                column: "TournamentId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentSC2Groups_TournamentId",
                table: "TournamentSC2Groups",
                column: "TournamentId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentSC2Matches_GroupId",
                table: "TournamentSC2Matches",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentSC2Matches_Player1Id",
                table: "TournamentSC2Matches",
                column: "Player1Id");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentSC2Matches_Player2Id",
                table: "TournamentSC2Matches",
                column: "Player2Id");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentSC2Matches_TournamentId",
                table: "TournamentSC2Matches",
                column: "TournamentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "BellumGensPushSubscriptions");

            migrationBuilder.DropTable(
                name: "CSGOMatchMaps");

            migrationBuilder.DropTable(
                name: "JerseyDetails");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "SC2MatchMaps");

            migrationBuilder.DropTable(
                name: "StrategyComments");

            migrationBuilder.DropTable(
                name: "StrategyVotes");

            migrationBuilder.DropTable(
                name: "Subscribers");

            migrationBuilder.DropTable(
                name: "TeamApplications");

            migrationBuilder.DropTable(
                name: "TeamAvailabilities");

            migrationBuilder.DropTable(
                name: "TeamInvites");

            migrationBuilder.DropTable(
                name: "TeamMapPools");

            migrationBuilder.DropTable(
                name: "TeamMembers");

            migrationBuilder.DropTable(
                name: "TournamentApplications");

            migrationBuilder.DropTable(
                name: "UserAvailabilities");

            migrationBuilder.DropTable(
                name: "UserMapPool");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "TournamentCSGOMatches");

            migrationBuilder.DropTable(
                name: "JerseyOrders");

            migrationBuilder.DropTable(
                name: "TournamentSC2Matches");

            migrationBuilder.DropTable(
                name: "CSGOStrategies");

            migrationBuilder.DropTable(
                name: "Companies");

            migrationBuilder.DropTable(
                name: "TournamentCSGOGroups");

            migrationBuilder.DropTable(
                name: "PromoCodes");

            migrationBuilder.DropTable(
                name: "TournamentSC2Groups");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "CSGOTeams");

            migrationBuilder.DropTable(
                name: "Tournaments");

            migrationBuilder.DropTable(
                name: "CSGODetails");

            migrationBuilder.DropTable(
                name: "StarCraft2Details");
        }
    }
}
