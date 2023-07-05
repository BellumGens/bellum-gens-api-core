using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BellumGens.Api.Core.Models
{
    // You can add profile data for the user by adding more properties to your ApplicationUser class, please visit https://go.microsoft.com/fwlink/?LinkID=317594 to learn more.
    public class ApplicationUser : IdentityUser
    {
        public string ESEA { get; set; }

        public bool SearchVisible { get; set; } = true;

		public string BattleNetId { get; set; }

		public string TwitchId { get; set; }
		
		public string SteamID { get; set; }

		public DateTimeOffset RegisteredOn { get; set; } = DateTimeOffset.Now;

		public DateTimeOffset LastSeen { get; set; } = DateTimeOffset.Now;

		public PlaystyleRole PreferredPrimaryRole { get; set; }

		public PlaystyleRole PreferredSecondaryRole { get; set; }

		[ForeignKey("SteamID")]
		public virtual CSGODetails CSGODetails { get; set; }

		[ForeignKey("BattleNetId")]
		public virtual StarCraft2Details StarCraft2Details { get; set; }

		public virtual ICollection<UserAvailability> Availability { get; set; } = new HashSet<UserAvailability>();

		public virtual ICollection<UserMapPool> MapPool { get; set; } = new HashSet<UserMapPool>();

		public virtual ICollection<TeamInvite> Notifications { get; set; } = new HashSet<TeamInvite>();

		public virtual ICollection<TeamMember> MemberOf { get; set; } = new HashSet<TeamMember>();

		public virtual ICollection<TeamApplication> TeamApplications { get; set; } = new HashSet<TeamApplication>();

        public virtual ICollection<TeamInvite> InvitesSent { get; set; } = new HashSet<TeamInvite>();
    }

	public class BellumGensDbContext : IdentityDbContext<ApplicationUser>
	{
		public DbSet<UserAvailability> UserAvailabilities { get; set; }

		public DbSet<CSGODetails> CSGODetails { get; set; }

		public DbSet<StarCraft2Details> StarCraft2Details { get; set; }

		public DbSet<UserMapPool> UserMapPool { get; set; }

		public DbSet<CSGOTeam> CSGOTeams { get; set; }

		public DbSet<TeamMember> TeamMembers { get; set; }

		public DbSet<TeamInvite> TeamInvites { get; set; }

		public DbSet<TeamApplication> TeamApplications { get; set; }

		public DbSet<CSGOStrategy> CSGOStrategies { get; set; }

		public DbSet<StrategyVote> StrategyVotes { get; set; }

		public DbSet<TeamMapPool> TeamMapPools { get; set; }

		public DbSet<TeamAvailability> TeamAvailabilities { get; set; }

		public DbSet<UserMessage> Messages { get; set; }

		public DbSet<BellumGensPushSubscription> BellumGensPushSubscriptions { get; set; }

		public DbSet<StrategyComment> StrategyComments { get; set; }

		public DbSet<Subscriber> Subscribers { get; set; }

        public DbSet<Tournament> Tournaments { get; set; }

        public DbSet<TournamentApplication> TournamentApplications { get; set; }

		public DbSet<TournamentCSGOGroup> TournamentCSGOGroups { get; set; }

		public DbSet<TournamentSC2Group> TournamentSC2Groups { get; set; }

		public DbSet<TournamentCSGOMatch> TournamentCSGOMatches { get; set; }

		public DbSet<CSGOMatchMap> CSGOMatchMaps { get; set; }

		public DbSet<TournamentSC2Match> TournamentSC2Matches { get; set; }

		public DbSet<SC2MatchMap> SC2MatchMaps { get; set; }

		public DbSet<Company> Companies { get; set; }

		public DbSet<JerseyOrder> JerseyOrders { get; set; }

		public DbSet<Promo> PromoCodes { get; set; }

        public BellumGensDbContext(DbContextOptions<BellumGensDbContext> options)
            : base(options)
        {
        }

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			modelBuilder.Entity<CSGODetails>()
						.Property(p => p.Accuracy)
						.HasPrecision(5, 2);

			modelBuilder.Entity<CSGODetails>()
						.Property(p => p.HeadshotPercentage)
						.HasPrecision(5, 2);

			modelBuilder.Entity<CSGODetails>()
						.Property(p => p.KillDeathRatio)
						.HasPrecision(4, 2);

			modelBuilder.Entity<Promo>()
						.Property(p => p.Discount)
						.HasPrecision(3, 2);

			modelBuilder.Entity<ApplicationUser>()
						.HasMany(e => e.Notifications)
						.WithOne(e => e.InvitedUser);

            modelBuilder.Entity<ApplicationUser>()
                        .HasMany(c => c.InvitesSent)
                        .WithOne(c => c.InvitingUser);

            modelBuilder.Entity<ApplicationUser>()
						.HasMany(e => e.TeamApplications)
						.WithOne(e => e.User);

			modelBuilder.Entity<CSGOStrategy>()
						.HasOne(s => s.Team)
						.WithMany(e => e.Strategies);

			modelBuilder.Entity<CSGOTeam>()
						.HasMany(e => e.Strategies)
						.WithOne(e => e.Team);

            modelBuilder.Entity<CSGOTeam>()
                        .HasIndex(c => c.CustomUrl)
                        .IsUnique();

			modelBuilder.Entity<CSGOTeam>()
						.HasIndex(c => c.SteamGroupId)
						.IsUnique();

            modelBuilder.Entity<Company>()
						.HasIndex(c => c.Name)
						.IsUnique();

			modelBuilder.Entity<CSGOStrategy>()
						.HasIndex(c => c.CustomUrl)
						.IsUnique();

			modelBuilder.Entity<Promo>()
						.HasIndex(c => c.Code)
						.IsUnique();

			modelBuilder.Entity<BellumGensPushSubscription>()
						.HasKey(c => new { c.P256dh, c.Auth });

			modelBuilder.Entity<StrategyVote>()
						.HasKey(c => new { c.StratId, c.UserId });

			modelBuilder.Entity<TeamApplication>()
						.HasKey(c => new { c.ApplicantId, c.TeamId });

			modelBuilder.Entity<TeamAvailability>()
						.HasKey(c => new { c.TeamId, c.Day });

			modelBuilder.Entity<UserAvailability>()
						.HasKey(c => new { c.UserId, c.Day });

			modelBuilder.Entity<TeamMapPool>()
						.HasKey(c => new { c.TeamId, c.Map });

			modelBuilder.Entity<UserMapPool>()
						.HasKey(c => new { c.UserId, c.Map });

			modelBuilder.Entity<TeamMember>()
						.HasKey(c => new { c.TeamId, c.UserId });

			modelBuilder.Entity<TeamInvite>()
						.HasOne(c => c.InvitingUser)
						.WithMany(c => c.InvitesSent)
						.OnDelete(DeleteBehavior.NoAction);

			modelBuilder.Entity<TeamInvite>()
						.HasOne(c => c.InvitedUser)
						.WithMany(c => c.Notifications)
                        .OnDelete(DeleteBehavior.NoAction);

			modelBuilder.Entity<TournamentCSGOMatch>()
						.HasOne(c => c.Team1)
						.WithMany()
						.OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<TournamentCSGOMatch>()
                        .HasOne(c => c.Team2)
                        .WithMany()
                        .OnDelete(DeleteBehavior.NoAction);
        }
	}
}