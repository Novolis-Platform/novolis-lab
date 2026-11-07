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
    public const string VersionLabel = "lab 2026.1";
}
