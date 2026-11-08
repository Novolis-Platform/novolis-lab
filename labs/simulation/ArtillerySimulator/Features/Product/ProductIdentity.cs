namespace ArtillerySimulator;

/// <summary>Player-facing product names. Levels live in <see cref="GameLevelCatalog"/>.</summary>
internal static class ProductIdentity
{
    public const string Title = "K9 DOG";
    /// <summary>Series subtitle — levels plug in under this, not into the product name.</summary>
    public const string Subtitle = "Howitzer defence simulation game";
    public const string WindowTitle = Title + " — " + Subtitle;

    public const string ShortPieceName = "K9 DOG";
    public const string PieceDisplayName = "K9 DOG (educational 155 mm L52 sandbox piece)";
    /// <summary>Stable catalog id — not a manufacturer SKU.</summary>
    public const string PieceSpecId = "k9.dog.educational";

    // --- Oslofjorden battery (public open-source ballpark stats; educational sandbox) ---

    public const string Oscarsborg28ShortName = "28 cm";
    public const string Oscarsborg28DisplayName =
        "Oscarsborg 28 cm coastal (educational — public 1890s Krupp / 9 Apr 1940 context)";
    public const string Oscarsborg28PieceSpecId = "oscarsborg.28cm.educational";

    public const string M777ShortName = "M777";
    public const string M777DisplayName =
        "M777 155 mm L39 (educational — public USMC/Army howitzer ballpark)";
    public const string M777PieceSpecId = "m777.155.educational";

    public const string HimarsShortName = "HIMARS";
    public const string HimarsDisplayName =
        "HIMARS GMLRS M31 (educational — public 6-pack unitary rocket ballpark)";
    public const string HimarsPieceSpecId = "himars.gmlrs.educational";

    public const string VersionLabel = "lab 2026.1";
}
