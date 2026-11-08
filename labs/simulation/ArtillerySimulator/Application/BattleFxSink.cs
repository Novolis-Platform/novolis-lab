using System.Numerics;

namespace ArtillerySimulator;

/// <summary>Presentation callbacks for battle events (particles, audio, camera).</summary>
internal sealed class BattleFxSink
{
    public Action<Vector3, Vector3>? Muzzle;
    public Action<Vector3, bool>? Burst;
    public Action? KillcamExpired;
    public Action<float, bool, IDefenseMission?>? FrameFx;

    public void OnMuzzle(Vector3 muzzle, Vector3 fireDir) => Muzzle?.Invoke(muzzle, fireDir);
    public void OnBurst(Vector3 at, bool airburst) => Burst?.Invoke(at, airburst);
    public void OnKillcamExpired() => KillcamExpired?.Invoke();
    public void OnFrameFx(float wallSeconds, bool audioPlaying, IDefenseMission? mission) =>
        FrameFx?.Invoke(wallSeconds, audioPlaying, mission);
}
