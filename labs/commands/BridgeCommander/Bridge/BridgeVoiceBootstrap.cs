using Novolis.Audio.Voice.SherpaOnnx;

namespace BridgeCommander.Bridge;

/// <summary>Ensures bundled Piper models are extracted from the SherpaOnnx nupkg.</summary>
internal static class BridgeVoiceBootstrap
{
    public static void EnsureBundledModelExtracted() =>
        BundledVoiceModelExtractor.EnsureAllExtracted(AppContext.BaseDirectory);
}
