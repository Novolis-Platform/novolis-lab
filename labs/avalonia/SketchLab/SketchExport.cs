using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Novolis.Avalonia.Controls.Sketch;

namespace SketchLab;

/// <summary>Transparent PNG / SVG exporters for <see cref="SketchDocument"/> (no file IO).</summary>
internal static class SketchExport
{
    const double Padding = 16;

    public static string ToSvg(SketchDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var bounds = ContentBounds(document.Elements);
        if (bounds is null)
        {
            return """
                <svg xmlns="http://www.w3.org/2000/svg" width="1" height="1" viewBox="0 0 1 1"/>
                """;
        }

        var b = bounds.Value;
        var ox = b.X - Padding;
        var oy = b.Y - Padding;
        var w = Math.Max(1, b.Width + Padding * 2);
        var h = Math.Max(1, b.Height + Padding * 2);
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{w:0.###}\" height=\"{h:0.###}\" viewBox=\"0 0 {w:0.###} {h:0.###}\">\n");

        foreach (var stroke in document.Elements)
        {
            if (stroke.Points.Count == 0)
                continue;
            var color = string.IsNullOrWhiteSpace(stroke.StrokeColor) ? "#1e1e1e" : stroke.StrokeColor;
            var width = stroke.StrokeWidth <= 0 ? 2 : stroke.StrokeWidth;
            if (stroke.Points.Count == 1)
            {
                var p = stroke.Points[0];
                sb.Append(CultureInfo.InvariantCulture,
                    $"  <circle cx=\"{p.X - ox:0.###}\" cy=\"{p.Y - oy:0.###}\" r=\"{Math.Max(0.25, width * 0.5):0.###}\" fill=\"{EscapeXml(color)}\"/>\n");
                continue;
            }

            var closed = stroke.Closed
                         || (stroke.Points.Count >= 3
                             && NearlyEqual(stroke.Points[0], stroke.Points[^1]));
            var fill = !string.IsNullOrWhiteSpace(stroke.FillColor) && closed
                ? EscapeXml(stroke.FillColor!)
                : "none";
            var dash = SketchStrokeStyles.SvgDashArray(stroke.StrokeStyle, width);
            var dashAttr = dash is null ? "" : $" stroke-dasharray=\"{dash}\"";
            var tag = closed ? "polygon" : "polyline";
            sb.Append(CultureInfo.InvariantCulture,
                $"  <{tag} fill=\"{fill}\" stroke=\"{EscapeXml(color)}\" stroke-width=\"{width:0.###}\" stroke-linecap=\"round\" stroke-linejoin=\"round\"{dashAttr} points=\"");
            var count = stroke.Points.Count;
            if (closed && count >= 2 && NearlyEqual(stroke.Points[0], stroke.Points[^1]))
                count--;
            for (var i = 0; i < count; i++)
            {
                var p = stroke.Points[i];
                if (i > 0)
                    sb.Append(' ');
                sb.Append(CultureInfo.InvariantCulture, $"{p.X - ox:0.###},{p.Y - oy:0.###}");
            }

            sb.Append(CultureInfo.InvariantCulture, $"/>\n");
        }

        sb.Append("</svg>\n");
        return sb.ToString();
    }

    /// <param name="opaqueBackground">
    /// When true (default for clipboard/ChatGPT), fills a white background so browsers accept the paste.
    /// When false, leaves pixels transparent.
    /// </param>
    public static byte[] ToPng(SketchDocument document, double scale = 2, bool opaqueBackground = true)
    {
        ArgumentNullException.ThrowIfNull(document);
        scale = Math.Clamp(scale, 0.5, 8);
        var bounds = ContentBounds(document.Elements);
        if (bounds is null)
            return EncodeEmptyPng(opaqueBackground);

        var b = bounds.Value;
        var worldW = Math.Max(1, b.Width + Padding * 2);
        var worldH = Math.Max(1, b.Height + Padding * 2);
        var pixelW = Math.Max(1, (int)Math.Ceiling(worldW * scale));
        var pixelH = Math.Max(1, (int)Math.Ceiling(worldH * scale));

        var surface = new StrokeExportSurface(
            document,
            b.X - Padding,
            b.Y - Padding,
            scale,
            opaqueBackground ? Colors.White : null)
        {
            Width = pixelW,
            Height = pixelH
        };
        surface.Measure(new Size(pixelW, pixelH));
        surface.Arrange(new Rect(0, 0, pixelW, pixelH));

        using var bitmap = new RenderTargetBitmap(new PixelSize(pixelW, pixelH), new Vector(96, 96));
        bitmap.Render(surface);
        using var ms = new MemoryStream();
        bitmap.Save(ms);
        return ms.ToArray();
    }

    static SketchRect? ContentBounds(IReadOnlyList<StrokeShape> elements)
    {
        SketchRect? union = null;
        foreach (var stroke in elements)
        {
            if (stroke.Points.Count == 0)
                continue;
            var b = SketchBounds.FromPoints(stroke.Points);
            var pad = Math.Max(1, stroke.StrokeWidth);
            b = b.Inflate(pad);
            union = union is null ? b : Union(union.Value, b);
        }

        return union;
    }

    static SketchRect Union(SketchRect a, SketchRect b)
    {
        var left = Math.Min(a.X, b.X);
        var top = Math.Min(a.Y, b.Y);
        var right = Math.Max(a.Right, b.Right);
        var bottom = Math.Max(a.Bottom, b.Bottom);
        return new SketchRect(left, top, right - left, bottom - top);
    }

    static byte[] EncodeEmptyPng(bool opaque)
    {
        var surface = new StrokeExportSurface(new SketchDocument(), 0, 0, 1, opaque ? Colors.White : null)
        {
            Width = 1,
            Height = 1
        };
        surface.Measure(new Size(1, 1));
        surface.Arrange(new Rect(0, 0, 1, 1));
        using var bitmap = new RenderTargetBitmap(new PixelSize(1, 1), new Vector(96, 96));
        bitmap.Render(surface);
        using var ms = new MemoryStream();
        bitmap.Save(ms);
        return ms.ToArray();
    }

    static bool NearlyEqual(SketchPoint a, SketchPoint b) =>
        Math.Abs(a.X - b.X) < 1e-6 && Math.Abs(a.Y - b.Y) < 1e-6;

    static string EscapeXml(string s) =>
        s.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

    sealed class StrokeExportSurface : Control
    {
        readonly SketchDocument _document;
        readonly double _originX;
        readonly double _originY;
        readonly double _scale;
        readonly Color? _background;

        public StrokeExportSurface(
            SketchDocument document,
            double originX,
            double originY,
            double scale,
            Color? background)
        {
            _document = document;
            _originX = originX;
            _originY = originY;
            _scale = scale;
            _background = background;
        }

        public override void Render(DrawingContext context)
        {
            if (_background is { } bg)
                context.FillRectangle(new ImmutableSolidColorBrush(bg), new Rect(Bounds.Size));

            foreach (var stroke in _document.Elements)
            {
                if (stroke.Points.Count == 0)
                    continue;
                Color color;
                try
                {
                    color = Color.Parse(string.IsNullOrWhiteSpace(stroke.StrokeColor) ? "#1e1e1e" : stroke.StrokeColor);
                }
                catch
                {
                    color = Color.Parse("#1e1e1e");
                }

                var thickness = Math.Max(0.15, stroke.StrokeWidth * _scale);
                var pen = new ImmutablePen(
                    new ImmutableSolidColorBrush(color),
                    thickness,
                    dashStyle: SketchStrokeStyles.CreateDash(stroke.StrokeStyle, thickness),
                    lineCap: PenLineCap.Round,
                    lineJoin: PenLineJoin.Round);

                if (stroke.Points.Count == 1)
                {
                    var p = Map(stroke.Points[0]);
                    var r = Math.Max(0.5, thickness * 0.5);
                    context.DrawEllipse(pen.Brush, null, p, r, r);
                    continue;
                }

                var closed = stroke.Closed
                             || (stroke.Points.Count >= 3 && NearlyEqual(stroke.Points[0], stroke.Points[^1]));
                IBrush? fill = null;
                if (!string.IsNullOrWhiteSpace(stroke.FillColor) && closed)
                {
                    try { fill = new ImmutableSolidColorBrush(Color.Parse(stroke.FillColor)); }
                    catch { fill = new ImmutableSolidColorBrush(color); }
                }

                var screen = new List<Point>(stroke.Points.Count + 1);
                foreach (var pt in stroke.Points)
                    screen.Add(Map(pt));
                if (closed && screen.Count >= 3)
                {
                    var a = screen[0];
                    var b = screen[^1];
                    if (Math.Abs(a.X - b.X) > 0.5 || Math.Abs(a.Y - b.Y) > 0.5)
                        screen.Add(a);
                }

                context.DrawGeometry(fill, pen, new PolylineGeometry(screen, isFilled: fill is not null));
            }
        }

        Point Map(SketchPoint p) =>
            new((p.X - _originX) * _scale, (p.Y - _originY) * _scale);
    }
}
