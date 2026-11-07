using System.Text;

namespace CalypsoCad.Structure;

/// <summary>
/// Fail-closed placement diagnostics — callers must record when primary geometry is omitted.
/// </summary>
internal static class StructurePlacementLog
{
    public sealed record Failure(
        string ComponentId,
        float Y,
        float Up,
        float ZStem,
        float HalfY,
        float HalfUp,
        float HalfZ,
        string Reason);

    private static readonly List<Failure> Failures = [];

    public static void Clear() => Failures.Clear();

    public static void Record(
        string componentId,
        float y,
        float up,
        float zStem,
        float halfY,
        float halfUp,
        float halfZ,
        string reason = "BoxFits rejected / omitted")
    {
        Failures.Add(new Failure(componentId, y, up, zStem, halfY, halfUp, halfZ, reason));
    }

    public static IReadOnlyList<Failure> All => Failures;

    public static int Count => Failures.Count;

    public static string WriteMarkdown(string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Structure placement failures");
        sb.AppendLine();
        sb.AppendLine($"Count: **{Failures.Count}**");
        sb.AppendLine();
        if (Failures.Count == 0)
        {
            sb.AppendLine("No omitted primary placements recorded.");
        }
        else
        {
            sb.AppendLine("| Id | Y | Up | Zstem | hy | hu | hz | Reason |");
            sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- |");
            foreach (var f in Failures)
            {
                sb.AppendLine(
                    $"| `{f.ComponentId}` | {f.Y:0.###} | {f.Up:0.###} | {f.ZStem:0.###} | {f.HalfY:0.###} | {f.HalfUp:0.###} | {f.HalfZ:0.###} | {f.Reason} |");
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, sb.ToString());
        return path;
    }
}
