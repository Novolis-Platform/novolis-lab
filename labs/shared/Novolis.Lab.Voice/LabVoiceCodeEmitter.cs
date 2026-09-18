using System.Globalization;
using System.Text;
using Novolis.Audio.Voice;
using Novolis.Audio.Voice.Design;

namespace Novolis.Lab.Voice;

/// <summary>Emits dogfood-specific C# snippets (ATC delivery, Bridge characters).</summary>
public static class LabVoiceCodeEmitter
{
    /// <summary>Emits code for the given dogfood template.</summary>
    public static string Emit(VoicePresetDraft draft, LabVoiceCodeTemplate template)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var validation = VoicePresetValidation.Validate(draft);
        if (!validation.IsValid)
            throw new InvalidOperationException(string.Join("; ", validation.Errors));

        return template switch
        {
            LabVoiceCodeTemplate.AtcDeliveryStatic => EmitAtcDelivery(draft),
            LabVoiceCodeTemplate.BridgeCharacter => EmitBridgeCharacter(draft),
            _ => throw new ArgumentOutOfRangeException(nameof(template)),
        };
    }

    /// <summary>Emits Sherpa usage with optional <see cref="AtcVoiceProfile.ApplyDelivery"/>.</summary>
    public static string EmitSherpaUsageWithAtc(VoicePresetDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var deliveryName = draft.ApplyRadioEffects || draft.UsePhraseology
            ? draft.PropertyName + "Delivery"
            : null;
        var rateLine = Math.Abs(draft.RateMultiplier - 1f) > 0.001f
            ? $$"""
                builder.Configure(o =>
                {
                    var s = o.Synthesis;
                    o.Synthesis = new VoiceSynthesisOptions
                    {
                        Profile = s.Profile,
                        ModelProfile = s.ModelProfile,
                        ModelDirectory = s.ModelDirectory,
                        SpeakingRate = s.SpeakingRate * {{FormatFloat(draft.RateMultiplier)}},
                    };
                });

                """
            : string.Empty;

        var atcLine = deliveryName is not null
            ? $"AtcVoiceProfile.ApplyDelivery(builder, {deliveryName});"
            : string.Empty;

        return $$"""
            using Novolis.Lab.Voice;
            using Novolis.Audio.Voice;
            using Novolis.Audio.Voice.Profiles;
            using Novolis.Audio.Voice.SherpaOnnx;

            var builder = VoiceArchetypeApplicator.Apply(
                new VoiceServiceBuilder().UseSherpaOnnx(),
                VoiceArchetypeCatalog.{{draft.PropertyName}});
            {{rateLine}}{{atcLine}}
            IVoiceService voice = builder.BuildService();
            await voice.SpeakAsync("Your phrase here.");
            """;
    }

    private static string EmitAtcDelivery(VoicePresetDraft draft)
    {
        var options = draft.ToAtcOptions();
        var defaults = new AtcVoiceOptions();
        var deliveryName = draft.PropertyName + "Delivery";
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"public static AtcVoiceOptions {deliveryName} {{ get; }} = new()");
        sb.AppendLine("{");

        if (!options.UsePhraseology)
            sb.AppendLine("    UsePhraseology = false,");

        if (!options.ApplyRadioEffects)
            sb.AppendLine("    ApplyRadioEffects = false,");

        if (!string.Equals(options.EffectChainId, defaults.EffectChainId, StringComparison.Ordinal))
            sb.AppendLine(CultureInfo.InvariantCulture, $"    EffectChainId = \"{options.EffectChainId}\",");

        if (Math.Abs(options.HighPassHz - defaults.HighPassHz) > 0.01f)
            sb.AppendLine(CultureInfo.InvariantCulture, $"    HighPassHz = {FormatFloat(options.HighPassHz)},");

        if (Math.Abs(options.LowPassHz - defaults.LowPassHz) > 0.01f)
            sb.AppendLine(CultureInfo.InvariantCulture, $"    LowPassHz = {FormatFloat(options.LowPassHz)},");

        if (Math.Abs(options.Drive - defaults.Drive) > 0.01f)
            sb.AppendLine(CultureInfo.InvariantCulture, $"    Drive = {FormatFloat(options.Drive)},");

        if (Math.Abs(options.MakeupGain - defaults.MakeupGain) > 0.01f)
            sb.AppendLine(CultureInfo.InvariantCulture, $"    MakeupGain = {FormatFloat(options.MakeupGain)},");

        if (Math.Abs(options.OutputGainDb - defaults.OutputGainDb) > 0.01f)
            sb.AppendLine(CultureInfo.InvariantCulture, $"    OutputGainDb = {FormatFloat(options.OutputGainDb)},");

        if (Math.Abs(options.HissLevel - defaults.HissLevel) > 0.0001f)
            sb.AppendLine(CultureInfo.InvariantCulture, $"    HissLevel = {FormatFloat(options.HissLevel)},");

        sb.AppendLine("};");
        return sb.ToString().TrimEnd();
    }

    private static string EmitBridgeCharacter(VoicePresetDraft draft)
    {
        var id = string.IsNullOrWhiteSpace(draft.BridgeCharacterId)
            ? draft.ProfileId.Replace('-', '_')
            : draft.BridgeCharacterId!;
        var deliveryName = draft.PropertyName + "Delivery";
        var hasDelivery = HasNonDefaultAtc(draft);
        var deliveryArg = hasDelivery ? $",\n        Delivery: {deliveryName}" : string.Empty;

        return $$"""
            public static BridgeCharacter {{draft.PropertyName}}Character { get; } = new(
                "{{id}}",
                "{{EscapeString(draft.BridgeDisplayName)}}",
                "{{draft.BridgeSpectreColor}}",
                VoiceArchetypeCatalog.{{draft.PropertyName}}{{deliveryArg}});
            """;
    }

    private static bool HasNonDefaultAtc(VoicePresetDraft draft)
    {
        var o = draft.ToAtcOptions();
        var d = new AtcVoiceOptions();
        return !o.UsePhraseology
            || !o.ApplyRadioEffects
            || Math.Abs(o.Drive - d.Drive) > 0.01f
            || Math.Abs(o.OutputGainDb - d.OutputGainDb) > 0.01f
            || Math.Abs(o.HissLevel - d.HissLevel) > 0.0001f
            || Math.Abs(o.HighPassHz - d.HighPassHz) > 0.01f
            || Math.Abs(o.LowPassHz - d.LowPassHz) > 0.01f;
    }

    private static string EscapeString(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    private static string FormatFloat(float value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture) + "f";
}
