using Novolis.Silk;

namespace ArtillerySimulator;

/// <summary>Routes defense mission scene lines into <see cref="DefenseSceneDraw"/> via <see cref="IDefenseMission.Draw"/>.</summary>
internal static class DefenseWireBridge
{
    public static void Draw(SilkWireBatch wires, IDefenseMission? mission) => mission?.Draw(wires);
}
