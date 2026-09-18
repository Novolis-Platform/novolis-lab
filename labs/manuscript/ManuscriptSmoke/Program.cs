using Novolis.Manuscript;
using Novolis.Manuscript.Export.Audio;

const string chapter = """
    # Chapter 3 - Quiet Harbor

    > [!date] 2495.042
    > [!pov] Ryn
    > [!system] Haven
    > [!status] draft

    The docks smelled of ozone and wet rope. Ryn counted the freighters again.

    ***

    Below decks, the chrono-lock hummed. Someone had rewritten the jump table overnight.
    """;

var (meta, _, format) = ManuscriptMetadata.Parse(chapter);
var words = ManuscriptMetadata.CountWords(chapter);

Console.WriteLine($"Metadata: format={format} title={meta.Title} date={meta.Date} pov={meta.Pov} system={meta.System} status={meta.Status}");
Console.WriteLine($"Words: {words}");

var plan = SpeechPlanner.Create(chapter, new SpeechOptions
{
    SceneBreakMs = 900,
    MaxChunkChars = 2800,
    Pronunciation = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Ryn"] = "Rin",
        ["chrono-lock"] = "chrono lock"
    }
}, speakTitle: false);

Console.WriteLine($"Speech plan: {plan.Segments.Count} segments, hash={plan.PlanHash[..12]}…");
foreach (var (segment, i) in plan.Segments.Select((s, i) => (s, i)))
{
    if (segment.Kind == SpeechSegmentKind.Pause)
        Console.WriteLine($"  [{i}] pause {segment.PauseMs} ms");
    else
        Console.WriteLine($"  [{i}] speak ({segment.Text?.Length ?? 0} chars): {Truncate(segment.Text, 72)}");
}

Console.WriteLine("ManuscriptSmoke OK");
return 0;

static string Truncate(string? text, int max)
{
    if (string.IsNullOrEmpty(text))
        return "";
    return text.Length <= max ? text : text[..(max - 1)] + "…";
}
