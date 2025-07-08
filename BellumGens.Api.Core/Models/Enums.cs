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
    public enum ProductType
    {
        Jersey,
        Umbrella,
        Pen,
        Pin,
        Bracelet
    }
    public enum SC2Race
    {
        Random,
        Protoss,
        Terran,
        Zerg
    }
}