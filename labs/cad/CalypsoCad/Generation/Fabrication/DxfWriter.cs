using System.Globalization;
using System.Text;

namespace CalypsoCad.Generation.Fabrication;

internal static class DxfWriter
{
    public static void WritePolyline(
        string path,
        IReadOnlyList<(float X, float Y)> outer,
        IReadOnlyList<IReadOnlyList<(float X, float Y)>>? holes = null) =>
        WritePolylines(path, [outer], holes);

    public static void WritePolylines(
        string path,
        IReadOnlyList<IReadOnlyList<(float X, float Y)>> outers,
        IReadOnlyList<IReadOnlyList<(float X, float Y)>>? holes = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("0");
        sb.AppendLine("SECTION");
        sb.AppendLine("2");
        sb.AppendLine("ENTITIES");
        foreach (var outer in outers)
            AppendLwPolyline(sb, outer, closed: true);
        if (holes is not null)
        {
            foreach (var hole in holes)
                AppendLwPolyline(sb, hole, closed: true);
        }

        sb.AppendLine("0");
        sb.AppendLine("ENDSEC");
        sb.AppendLine("0");
        sb.AppendLine("EOF");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, sb.ToString());
    }

    private static void AppendLwPolyline(StringBuilder sb, IReadOnlyList<(float X, float Y)> pts, bool closed)
    {
        sb.AppendLine("0");
        sb.AppendLine("LWPOLYLINE");
        sb.AppendLine("8");
        sb.AppendLine("CUT");
        sb.AppendLine("90");
        sb.AppendLine(pts.Count.ToString(CultureInfo.InvariantCulture));
        sb.AppendLine("70");
        sb.AppendLine(closed ? "1" : "0");
        foreach (var (x, y) in pts)
        {
            sb.AppendLine("10");
            sb.AppendLine(x.ToString("G9", CultureInfo.InvariantCulture));
            sb.AppendLine("20");
            sb.AppendLine(y.ToString("G9", CultureInfo.InvariantCulture));
        }
    }
}
