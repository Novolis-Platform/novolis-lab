using Novolis.Silk;

namespace ArtillerySimulator;

internal static class ProfileHotkeys
{
    public sealed class HotkeyResult
    {
        public bool ToggledShowProfile { get; init; }
        public bool ResetLiveProfiler { get; init; }
        public string? ProfileFlash { get; init; }
        public bool ForceShowProfile { get; init; }
    }

    public static HotkeyResult? TryHandle(
        SilkFrame frame,
        FrameProfiler profiler,
        ArtillerySceneRenderer renderer,
        BattleWorld world)
    {
        if (frame.IsKeyPressed(Key.F1))
        {
            return new HotkeyResult
            {
                ToggledShowProfile = true,
                ResetLiveProfiler = true,
            };
        }

        var ctrl = frame.IsKeyDown(Key.ControlLeft) || frame.IsKeyDown(Key.ControlRight);
        if (!ctrl || !frame.IsKeyPressed(Key.C))
            return null;

        var dump = ArtilleryDebugDump.BuildClipboardDump(
            frame,
            profiler,
            renderer,
            world.Terrain,
            world.Atmosphere,
            world.Gun,
            world.Crew,
            world.Rounds,
            world.Camera,
            world.Mission);
        var ok = WindowsClipboardText.TrySet(dump);
        return new HotkeyResult
        {
            ProfileFlash = ok ? "DEBUG DUMP COPIED" : "CLIPBOARD FAILED",
            ForceShowProfile = true,
            ResetLiveProfiler = true,
        };
    }
}
