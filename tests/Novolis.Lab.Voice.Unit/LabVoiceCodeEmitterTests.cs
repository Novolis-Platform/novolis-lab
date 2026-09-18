using Novolis.Audio.Voice.Design;
using Novolis.Audio.Voice.Profiles;
using Novolis.Lab.Voice;

namespace Novolis.Lab.Voice.Unit;

public class LabVoiceCodeEmitterTests
{
    [Test]
    public async Task EmitAtcDelivery_omits_defaults()
    {
        var draft = VoicePresetDraft.FromArchetype(VoiceArchetypeCatalog.NeutralFemale);
        draft.PropertyName = "DryNeutral";
        draft.ApplyRadioEffects = false;
        draft.UsePhraseology = false;
        foreach (var step in draft.EffectSteps)
        {
            if (step.Kind == VoiceEffectStepKind.Phraseology)
                step.Enabled = false;
        }

        var code = LabVoiceCodeEmitter.Emit(draft, LabVoiceCodeTemplate.AtcDeliveryStatic);

        await Assert.That(code).Contains("DryNeutralDelivery");
        await Assert.That(code).Contains("UsePhraseology = false");
        await Assert.That(code).Contains("ApplyRadioEffects = false");
        await Assert.That(code).DoesNotContain("Drive =");
    }

    [Test]
    public async Task EmitAtcDelivery_includes_non_default_dsp()
    {
        var draft = VoicePresetDraft.FromArchetype(VoiceArchetypeCatalog.ExcitableFemale);
        draft.PropertyName = "Urgent";
        draft.Drive = 3.2f;
        draft.OutputGainDb = 6f;

        var code = LabVoiceCodeEmitter.Emit(draft, LabVoiceCodeTemplate.AtcDeliveryStatic);

        await Assert.That(code).Contains("Drive = 3.2f");
        await Assert.That(code).Contains("OutputGainDb = 6f");
    }

    [Test]
    public async Task EmitBridgeCharacter_references_archetype_and_delivery()
    {
        var draft = VoicePresetDraft.FromArchetype(VoiceArchetypeCatalog.SteadyMale);
        draft.PropertyName = "Helm";
        draft.BridgeCharacterId = "helm";
        draft.BridgeDisplayName = "Helm";
        draft.Drive = 2.6f;

        var code = LabVoiceCodeEmitter.Emit(draft, LabVoiceCodeTemplate.BridgeCharacter);

        await Assert.That(code).Contains("HelmCharacter");
        await Assert.That(code).Contains("VoiceArchetypeCatalog.Helm");
        await Assert.That(code).Contains("HelmDelivery");
    }
}
