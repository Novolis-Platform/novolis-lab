namespace ArtillerySimulator;

/// <summary>Legacy HUD entry — chrome lives in <see cref="FireControlUi"/>.</summary>
internal static class SimulationHud
{
    public static int LastOverlayLineCount => FireControlUi.LastOverlayLineCount;
}
