using Novolis.Audio.Voice;
using Novolis.Lab.Voice;
using Novolis.Audio.Voice.Profiles;
using Novolis.Audio.Voice.SherpaOnnx;

var useNull = args.Contains("--null", StringComparer.OrdinalIgnoreCase);
if (!useNull)
    BundledVoiceModelExtractor.EnsureAllExtracted(AppContext.BaseDirectory);
var writeWav = args.Contains("--wav", StringComparer.OrdinalIgnoreCase);
var speakOnly = args.Contains("--speak-only", StringComparer.OrdinalIgnoreCase);
var calm = args.Contains("--calm", StringComparer.OrdinalIgnoreCase);

VoiceArchetype archetype = calm
    ? VoiceArchetypeCatalog.NeutralFemale
    : VoiceArchetypeCatalog.ExcitableFemale;

AtcVoiceOptions? delivery = calm
    ? null
    : new AtcVoiceOptions
    {
        Drive = 3.2f,
        OutputGainDb = 6f,
        HissLevel = 0.005f,
    };

IVoiceService voice;
if (useNull)
{
    voice = new VoiceServiceBuilder().UseNullSynthesizer().UseNullPlayback().BuildService();
}
else
{
    var builder = VoiceArchetypeApplicator.Apply(new VoiceServiceBuilder().UseSherpaOnnx(), archetype);
    if (delivery is not null)
        AtcVoiceProfile.ApplyDelivery(builder, delivery);
    voice = builder.BuildService();
}

var paths = SherpaVoiceModelPaths.TryResolve(modelDirectory: null, archetype.Model);
Console.WriteLine(paths is null
    ? DescribeMissingModel(AppContext.BaseDirectory, archetype.Model.Id)
    : $"Voice: Sherpa model {paths.ProfileId} @ {paths.SampleRateHz} Hz ({paths.ModelDirectory})");
Console.WriteLine(calm
    ? $"Profile: {archetype.Profile.Id} (dry, rate {archetype.SpeakingRate:0.00}x)"
    : $"Profile: {archetype.Profile.Id} + ATC radio (rate {archetype.SpeakingRate:0.00}x, drive {delivery!.Drive:0.0})");

var samples = calm
    ? new[]
    {
        "Tower, ready for departure.",
        "Comms: hail transmitted on open channel.",
        "SAS one two three climbing flight level three five zero.",
    }
    : new[]
    {
        "NOVAMICS TOWER, NOVAMICS one two three, request immediate departure runway two two!",
        "Contact departure on one two four decimal three two five, expedite climb, good day.",
        "MAYDAY MAYDAY MAYDAY, NOVAMICS one two three, declaring emergency, request vectors nearest suitable.",
    };

foreach (var text in samples)
{
    Console.WriteLine($"Speak: {text}");
    await voice.SpeakAsync(text);
}

if (writeWav && !speakOnly)
{
    var wavPath = Path.Combine(Path.GetTempPath(), "novolis-voice-smoke.wav");
    await voice.WriteToFileAsync(samples[^1], new FileInfo(wavPath));
    Console.WriteLine($"Wrote {wavPath}");
}

Console.WriteLine("VoiceSmoke OK");
return 0;

static string DescribeMissingModel(string baseDir, string profileId)
{
    var profileDir = Path.Combine(baseDir, "models", profileId);
    var flatDir = Path.Combine(baseDir, "models");
    if (!Directory.Exists(flatDir) && !Directory.Exists(profileDir))
        return "Voice: null/silent fallback (no models/ under output — rebuild VoiceSmoke after restore).";

    var onnx = Directory.GetFiles(profileDir, "*.onnx", SearchOption.TopDirectoryOnly)
        .Concat(Directory.Exists(flatDir) ? Directory.GetFiles(flatDir, "*.onnx") : [])
        .FirstOrDefault();
    if (onnx is not null && VoiceModelMaterialization.IsGitLfsPointer(onnx))
        return "Voice: null/silent fallback (ONNX is a Git LFS pointer — run: git lfs pull in novolis-audio).";

    var phontab = Path.Combine(profileDir, "espeak-ng-data", "phontab");
    if (Directory.Exists(phontab) || (File.Exists(phontab) is false && Directory.Exists(Path.Combine(profileDir, "espeak-ng-data"))))
        return "Voice: null/silent fallback (stale/broken model extract — dotnet clean and rebuild VoiceSmoke).";

    return $"Voice: null/silent fallback (need models/{profileId} from Novolis.Audio.Voice.SherpaOnnx on GitHub Packages).";
}
