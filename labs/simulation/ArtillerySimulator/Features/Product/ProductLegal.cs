namespace ArtillerySimulator;

/// <summary>In-product disclaimers. License overlay loads the full shipped NOTICE texts.</summary>
internal static class ProductLegal
{
    public const string Tagline =
        "Educational sandbox. Fiction. Not affiliated with any manufacturer, armed force, or government.";

    public const string PauseHint =
        "Esc / P  resume     L  licenses     Enter  resume";

    public const string SplashControls =
        "← →  change level     Enter / Space  begin     L  licenses";

    public const string LegalCloseHint =
        "↑↓ / W S / wheel  scroll     Esc / L  close";

    public static string SplashFooter =>
        $"{ProductIdentity.VersionLabel}  ·  {Tagline}";

    private static string? _fullNotices;

    /// <summary>Full license panel: active level + NOTICE.txt + sfx/LICENSE.txt.</summary>
    public static string BuildLicensePanel(IGameLevel? level)
    {
        var levelLine = level is null
            ? "Active level: (none selected yet)."
            : $"Active level: {level.DisplayName} ({level.Id}) — {level.SiteName}.";

        return $"""
            {ProductIdentity.Title} — {ProductIdentity.Subtitle}
            {ProductIdentity.VersionLabel}
            {levelLine}

            {LegalCloseHint}

            {LoadFullNotices()}
            """;
    }

    public static string LoadFullNotices()
    {
        if (_fullNotices is not null)
            return _fullNotices;

        var parts = new List<string>(3);
        TryAppendAsset(parts, "assets/NOTICE.txt");
        TryAppendAsset(parts, "assets/sfx/LICENSE.txt", header: "SFX LICENSE FILE (assets/sfx/LICENSE.txt)");
        if (parts.Count == 0)
        {
            _fullNotices =
                "NOTICE.txt was not found next to the executable.\n"
                + "Rebuild so assets/NOTICE.txt and assets/sfx/LICENSE.txt copy to output.";
            return _fullNotices;
        }

        _fullNotices = string.Join("\n\n", parts);
        return _fullNotices;
    }

    private static void TryAppendAsset(List<string> parts, string relative, string? header = null)
    {
        var path = Path.Combine(AppContext.BaseDirectory, relative);
        if (!File.Exists(path))
            return;

        try
        {
            var text = File.ReadAllText(path).Trim();
            if (text.Length == 0)
                return;
            parts.Add(header is null ? text : $"{header}\n{new string('-', header.Length)}\n{text}");
        }
        catch
        {
            // Missing or unreadable — skip; caller may still have NOTICE.
        }
    }
}
