namespace ArtillerySimulator;

internal static class ShellOverlayTitle
{
    public static string For(ShellPhase phase, IGameLevel? level)
    {
        var levelTitle = level is null
            ? ProductIdentity.WindowTitle
            : $"{ProductIdentity.Title} — {level.DisplayName}";

        return phase switch
        {
            ShellPhase.Playing => levelTitle,
            ShellPhase.Paused => $"{levelTitle} (paused)",
            ShellPhase.Legal => $"{ProductIdentity.Title} — licenses",
            ShellPhase.Loading => $"{ProductIdentity.Title} — loading",
            _ => ProductIdentity.WindowTitle,
        };
    }
}
