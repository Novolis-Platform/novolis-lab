using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Novolis.Pdf.Abstractions;
using Novolis.Pdf.Parsing;
using Novolis.Pdf.Text;

namespace Novolis.Governance.InspectNorgeskartPdf;

internal static class NorgeskartPdfSpanDump
{
    private static readonly Regex UtmEasting = new(@"\b(\d{5,7})\b", RegexOptions.Compiled);
    private static readonly Regex LatLon = new(
        @"(\d{2})[°\s]+(\d{1,2})[,\.]?(\d{0,2})?\s*([NnSs])|(\d{1,2})[°\s]+(\d{1,2})[,\.]?(\d{0,2})?\s*([EeWw])",
        RegexOptions.Compiled);

    internal static void Dump(PdfParsedDocument document, string path)
    {
        var spans = new PdfTextExtractor().ExtractPage(document, 0);
        var sb = new StringBuilder();
        sb.AppendLine($"# spans={spans.Count}");
        foreach (var span in spans.OrderByDescending(s => s.Bounds.Top).ThenBy(s => s.Bounds.Left))
        {
            var t = span.Text.Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (t.Length == 0)
                continue;
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"{span.Bounds.Left:F1},{span.Bounds.Bottom:F1}–{span.Bounds.Right:F1},{span.Bounds.Top:F1} size={span.FontSize:F1} | {t}");
        }

        File.WriteAllText(path, sb.ToString());
        Console.WriteLine($"Wrote {spans.Count} spans → {path}");

        var joined = string.Concat(spans.Select(s => s.Text));
        foreach (Match m in UtmEasting.Matches(joined))
            Console.WriteLine($"  utm-candidate: {m.Value}");
    }
}
