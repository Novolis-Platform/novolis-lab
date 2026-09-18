using Novolis.Audio.Effects;
using Novolis.Audio.Voice;
using Novolis.Audio.Voice.Profiles;
using Novolis.Lab.Voice;

namespace Novolis.Lab.Voice.Unit;

public class AtcVoiceProfileTests
{
    [Test]
    public async Task AtcVoiceProfile_exposes_delivery_tag()
    {
        await Assert.That(AtcVoiceProfile.DeliveryTag.Id).IsEqualTo("atc");
    }

    [Test]
    public async Task AtcVoiceProfile_ApplyDelivery_preserves_archetype_model()
    {
        var builder = VoiceArchetypeApplicator.Apply(
            new VoiceServiceBuilder(),
            VoiceArchetypeCatalog.ExcitableFemale);
        AtcVoiceProfile.ApplyDelivery(builder, new AtcVoiceOptions { Drive = 3.1f });

        await Assert.That(builder.SynthesisOptions.ModelProfile).IsEqualTo(VoiceModelCatalog.EnUsPiperAmy);
        await Assert.That(builder.SynthesisOptions.Profile.Id).IsEqualTo("excitable_female");
        await Assert.That(builder.SynthesisOptions.SpeakingRate).IsEqualTo(1.48f);
        await Assert.That(builder.EffectPipeline.GetType() == typeof(IdentityEffectPipeline)).IsFalse();
    }

    [Test]
    public async Task AtcVoiceProfile_ApplyDelivery_uses_model_sample_rate_for_radio()
    {
        var builder = VoiceArchetypeApplicator.Apply(
            new VoiceServiceBuilder(),
            VoiceArchetypeCatalog.CalmFemale);
        AtcVoiceProfile.ApplyDelivery(builder);

        await Assert.That(VoiceModelCatalog.TryGet(builder.SynthesisOptions.ModelProfile, out var model)).IsTrue();
        await Assert.That(model.SampleRateHz).IsEqualTo(22_050);
    }
}
