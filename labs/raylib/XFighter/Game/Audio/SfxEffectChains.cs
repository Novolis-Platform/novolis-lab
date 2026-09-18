using Novolis.Audio.Core;
using Novolis.Audio.Effects;
using Novolis.Audio.Filters;

namespace XFighter.Game.Audio;

/// <summary>Novolis.Audio.Effects pipelines for combat SFX.</summary>
internal static class SfxEffectChains
{
    private static readonly int Rate = ProceduralSfx.Format.SampleRate;

    public static readonly IAudioEffectPipeline Laser = new ChainedEffectPipeline(
        new BandLimitEffect(Rate, 280f, 9_500f),
        new DynamicsEffect(drive: 2.1f, makeupGain: 1.05f),
        GainEffect.FromDecibels(-2f));

    public static readonly IAudioEffectPipeline Explosion = new ChainedEffectPipeline(
        new BandLimitEffect(Rate, 40f, 6_000f),
        new DynamicsEffect(drive: 2.8f, makeupGain: 1.2f),
        GainEffect.FromDecibels(1f));

    public static readonly IAudioEffectPipeline Shield = new ChainedEffectPipeline(
        new BandLimitEffect(Rate, 200f, 4_500f),
        GainEffect.FromDecibels(-1f));

    public static readonly IAudioEffectPipeline EnemyBolt = new ChainedEffectPipeline(
        new BandLimitEffect(Rate, 350f, 7_000f),
        new DynamicsEffect(drive: 1.9f, makeupGain: 1f),
        GainEffect.FromDecibels(-3f));

    public static readonly IAudioEffectPipeline Radio = new ChainedEffectPipeline(
        new BandLimitEffect(Rate, 320f, 3_400f),
        new DynamicsEffect(drive: 2.4f, makeupGain: 1.1f),
        new RadioHissEffect(0.007f));

    public static readonly IAudioEffectPipeline Music = new ChainedEffectPipeline(
        new BandLimitEffect(Rate, 60f, 12_000f),
        GainEffect.FromDecibels(-14f));
}
