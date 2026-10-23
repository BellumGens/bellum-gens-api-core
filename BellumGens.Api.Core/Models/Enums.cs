using System.Text.Json.Serialization;

namespace BellumGens.Api.Core.Models
{
	public enum Game
	{
		CSGO,
		StarCraft2
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

	public enum PlaystyleRole
	{
		NotSet,
		IGL,
		Awper,
		EntryFragger,
		Support,
		Lurker
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
		Refunded
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