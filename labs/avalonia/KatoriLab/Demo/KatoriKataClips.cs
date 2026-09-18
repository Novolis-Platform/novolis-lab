namespace KatoriLab.Demo;

/// <summary>Phase labels / seek times — motion lives in <see cref="KenTimeline"/>.</summary>
internal static class KatoriKataClips
{
    public const string ClipId = "katori-ken";
    public const float Duration = KenTimeline.Duration;

    public static readonly (string Id, float Time, string Label)[] Phases = KenTimeline.Phases;

    public static float TimeForPhase(string phase) => KenTimeline.TimeForPhase(phase);

    public static string PhaseName(float timeInClip) => KenTimeline.PhaseName(timeInClip);
}
