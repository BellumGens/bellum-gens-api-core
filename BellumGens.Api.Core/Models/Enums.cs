using System.Text.Json.Serialization;

namespace BellumGens.Api.Core.Models
{
	public enum Game
	{
		CSGO,
		StarCraft2,
		Dota2
	}

	public enum NotificationState
	{
		NotSeen,
		Seen,
		Rejected,
		Accepted
	}

	public enum Side
	{
		TSide,
		CTSide
	}

	/// <summary>
	/// The roles of a team roster. Counter-Strike and Dota 2 rosters each have their own five roles;
	/// <see cref="PlaystyleRoleExtensions.IsRoleFor"/> tells which game a role belongs to.
	/// </summary>
	public enum PlaystyleRole
	{
		NotSet,
		// Counter-Strike
		IGL,
		Awper,
		EntryFragger,
		Support,
		Lurker,
		// Dota 2 positions 1 to 5
		Carry,
		Mid,
		Offlane,
		SoftSupport,
		HardSupport
	}

	public static class PlaystyleRoleExtensions
	{
		/// <summary>The game whose rosters have the role; null for <see cref="PlaystyleRole.NotSet"/>.</summary>
		public static Game? RoleGame(this PlaystyleRole role)
		{
			return role switch
			{
				PlaystyleRole.IGL or PlaystyleRole.Awper or PlaystyleRole.EntryFragger or PlaystyleRole.Support or PlaystyleRole.Lurker => Game.CSGO,
				PlaystyleRole.Carry or PlaystyleRole.Mid or PlaystyleRole.Offlane or PlaystyleRole.SoftSupport or PlaystyleRole.HardSupport => Game.Dota2,
				_ => null
			};
		}

		/// <summary>Whether a member of a team playing <paramref name="game"/> can take the role. Any team can leave a role unset.</summary>
		public static bool IsRoleFor(this PlaystyleRole role, Game game)
		{
			return role == PlaystyleRole.NotSet || role.RoleGame() == game;
		}

		/// <summary>Teams are formed for Counter-Strike and Dota 2; StarCraft II is played by single players.</summary>
		public static bool IsTeamGame(this Game game)
		{
			return game == Game.CSGO || game == Game.Dota2;
		}
	}

	public enum VoteDirection
	{
		Up,
		Down
	}

	public enum CSGOMap
	{
		Cache,
		Dust2,
		Inferno,
		Mirage,
		Nuke,
		Overpass,
		Train,
		Vertigo,
		Cobblestone,
		Ancient,
		Anubis
	}

	public enum SC2Map
	{
		TritonLE,
		EphemeronLE,
		WorldofSleepersLE,
		ZenLE,
		SimulacrumLE,
		NightshadeLE,
		EternalEmpireLE,
		GoldenWallLE,
		PurityAndIndustryLE,
		EverDreamLE,
		SubmarineLE,
		DeathauraLE,
		PillarsofGoldLE,
		OxideLE,
		LightshadeLE,
		RomanticideLE,
		JagannathaLE,
		IceAndChromeLE,
		AlcyoneLE,
		AmphionLE,
		CrimsonCourtLE,
		DynastyLE,
		GhostRiverLE,
		GoldenauraLE,
		OceanbornLE,
		PostYouthLE,
		SiteDeltaLE,
		AbyssalReefLE,
		AmygdalaLE,
		ElDoradoLE,
		FrostlineLE,
		KingsCoveLE,
		LeyLinesLE,
		NeonVioletSquareLE,
		UltraloveLE,
		WhispersofGoldLE
	}

	public enum TournamentApplicationState
	{
		Pending,
		Confirmed,
		Banned
	}

	public enum JerseyCut
	{
		Male,
		Female
	}

	public enum JerseySize
	{
		XS,
		S,
		M,
		L,
		XL,
		XXL,
		XXXL
	}
	public enum SC2Race
	{
		Random,
		Protoss,
		Terran,
		Zerg
	}

	// Shop

	public enum ProductType
	{
		Jersey,
		Umbrella,
		Pen,
		Pin,
		Bracelet,
		Other
	}

	public enum Brand
	{
		BellumGens,
		EBLeague,
		BGEStaraZagora
	}

	public enum OrderStatus
	{
		AwaitingPayment,
		Paid,
		Shipped,
		Delivered,
		Cancelled,
		Refunded,
		/// <summary>Placed with cash on delivery: nothing is paid online, the courier collects the payment.</summary>
		Confirmed
	}

	public enum PaymentStatus
	{
		Pending,
		Authorised,
		Completed,
		Failed,
		Cancelled
	}

	public enum PaymentProvider
	{
		Revolut
	}

	public enum PaymentMethod
	{
		Revolut,
		CashOnDelivery
	}

	public enum DeliveryMethod
	{
		Courier,
		EventPickup
	}

	/// <summary>Serialized as the kebab-case strings the client uses ("draft", "in-progress", ...); stored as int.</summary>
	[JsonConverter(typeof(JsonStringEnumConverter<TournamentStatus>))]
	public enum TournamentStatus
	{
		[JsonStringEnumMemberName("draft")]
		Draft,
		[JsonStringEnumMemberName("open")]
		Open,
		[JsonStringEnumMemberName("in-progress")]
		InProgress,
		[JsonStringEnumMemberName("completed")]
		Completed,
		[JsonStringEnumMemberName("cancelled")]
		Cancelled
	}

	/// <summary>Serialized as the kebab-case strings the client uses ("public", "private", "invite-only"); stored as int.</summary>
	[JsonConverter(typeof(JsonStringEnumConverter<TournamentVisibility>))]
	public enum TournamentVisibility
	{
		[JsonStringEnumMemberName("public")]
		Public,
		[JsonStringEnumMemberName("private")]
		Private,
		[JsonStringEnumMemberName("invite-only")]
		InviteOnly
	}
}