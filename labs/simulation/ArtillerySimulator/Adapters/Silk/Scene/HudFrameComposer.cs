using System.Numerics;
using Novolis.Silk;

namespace ArtillerySimulator;

/// <summary>Skia HUD pass for the playing frame (invoked after world draws).</summary>
internal static class HudFrameComposer
{
    public static void Draw(SilkFrame frame, FrameViewModel vm, float fps)
    {
        var predicted = vm.Aim.PredictedImpact();
        var predictedRange = vm.PredictedRangeMeters;
        var tof = vm.Aim.EstimatedTofSeconds();
        var title = vm.Mission is null
            ? $"{vm.Level.DisplayName} — {vm.Level.SiteName}"
            : $"{vm.Level.DisplayName.ToUpperInvariant()}  |  {vm.Mission.HudLine()}";

        vm.Ui.Draw(
            frame,
            vm.Gun,
            vm.Crew.OrderedElevation,
            vm.Crew.OrderedAzimuth,
            vm.Terrain,
            vm.Atmosphere,
            vm.Rounds.Active,
            vm.FollowProjectile,
            fps,
            predictedRange,
            tof,
            predicted,
            title,
            vm.FireBlockReason,
            vm.SolutionUnsafe,
            vm.LaunchDetonation,
            vm.Audio?.RadioLine,
            vm.Audio?.RadioSpeaker,
            vm.Audio?.VoiceAvailable == true,
            vm.Audio?.ScoreAvailable == true,
            audioHubPresent: vm.Audio is not null,
            score: vm.Score);
    }
}
