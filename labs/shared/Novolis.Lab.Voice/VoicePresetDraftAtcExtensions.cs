using Novolis.Audio.Voice.Design;

namespace Novolis.Lab.Voice;

/// <summary>Maps <see cref="VoicePresetDraft"/> effect steps to <see cref="AtcVoiceOptions"/> for dogfood apps.</summary>
public static class VoicePresetDraftAtcExtensions
{
    /// <summary>Builds ATC delivery options from the draft's effect chain and legacy flags.</summary>
    public static AtcVoiceOptions ToAtcOptions(this VoicePresetDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        draft.SyncLegacyFlagsFromSteps();
        var defaults = new AtcVoiceOptions();
        var band = FindStep(draft, VoiceEffectStepKind.BandLimit);
        var dynamics = FindStep(draft, VoiceEffectStepKind.Dynamics);
        var output = FindStep(draft, VoiceEffectStepKind.OutputGain);
        var hiss = FindStep(draft, VoiceEffectStepKind.RadioHiss);
        return new AtcVoiceOptions
        {
            UsePhraseology = draft.UsePhraseology,
            ApplyRadioEffects = draft.ApplyRadioEffects,
            EffectChainId = draft.ApplyRadioEffects ? draft.EffectChainId : "none",
            HighPassHz = band?.HighPassHz ?? draft.HighPassHz,
            LowPassHz = band?.LowPassHz ?? draft.LowPassHz,
            Drive = dynamics?.Drive ?? draft.Drive,
            MakeupGain = dynamics?.MakeupGain ?? draft.MakeupGain,
            OutputGainDb = output?.OutputGainDb ?? draft.OutputGainDb,
            HissLevel = hiss?.HissLevel ?? draft.HissLevel,
            EffectSampleRateHz = defaults.EffectSampleRateHz,
        };
    }

    private static VoiceDeliveryEffectStep? FindStep(VoicePresetDraft draft, VoiceEffectStepKind kind) =>
        draft.EffectSteps.FirstOrDefault(s => s.Enabled && s.Kind == kind);
}
