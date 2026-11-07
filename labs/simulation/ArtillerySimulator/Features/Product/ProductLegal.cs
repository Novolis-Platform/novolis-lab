namespace ArtillerySimulator;

/// <summary>In-product disclaimers and license blurbs. Keep short for HUD; full text lives in NOTICE.txt.</summary>
internal static class ProductLegal
{
    public const string Tagline =
        "Educational sandbox. Fiction. Not affiliated with any manufacturer, armed force, or government.";

    public const string DisclaimerShort =
        "K9 DOG is a cheeky lab name for a fictional educational howitzer. "
        + "It is not a manufacturer product and not an official armed-forces simulation. "
        + "Ballistic PE, MV, met, and warhead radii are open educational approximations — not firing tables, not classified data, not certification.";

    public const string MapDataDisclaimer =
        "Level maps may use public Kartverket / NVDB extracts under their terms. "
        + "Scenarios are fictional defense exercises, not targeting instruction.";

    public const string PauseHint =
        "Esc / P  resume     L  licenses     Enter  resume";

    public const string SplashControls =
        "← →  change level     Enter / Space  begin     L  licenses";

    public const string LegalCloseHint =
        "Esc / L  close";

    public static string SplashFooter =>
        $"{ProductIdentity.VersionLabel}  ·  {Tagline}";

    public static string BuildLicensePanel(IGameLevel? level)
    {
        var levelLine = level is null
            ? "Levels are pluggable via GameLevelCatalog."
            : $"Active level: {level.DisplayName} ({level.Id}) — {level.SiteName}.";

        return $"""
            {ProductIdentity.Title} — {ProductIdentity.Subtitle}
            {ProductIdentity.VersionLabel}

            DISCLAIMER
            {DisclaimerShort}

            MAP / SCENARIO
            {MapDataDisclaimer}
            {levelLine}

            LICENSES (summary)
            • This lab executable: Novolis / project license as shipped with the Novolis-Platform tree.
            • Silk.NET, SkiaSharp, .NET runtime: their respective OSS licenses (MIT / Apache-2.0 / etc.).
            • Kartverket NHM / NVDB road extracts: public data — redistribute only per provider terms.
            • Splash/icon art: Novolis lab assets for this product; not a third-party brand mark.

            LEVELS
            Implement IGameLevel and GameLevelCatalog.Register(...).
            Splash: ← → cycle. CLI: --list-levels / --level <id>.

            Full notices: NOTICE.txt next to the executable.
            {LegalCloseHint}
            """;
    }
}
