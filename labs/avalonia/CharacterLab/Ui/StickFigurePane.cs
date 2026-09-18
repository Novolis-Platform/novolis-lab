using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Novolis.Simulation.Humanoid;

namespace CharacterLab.Ui;

/// <summary>Avalonia viewport: capsule mannequin + optional debug sticks / overlays.</summary>
internal sealed class StickFigurePane : Control
{
    private MannequinCapsule[] _limbs = [];
    private Vector3? _head;
    private HumanoidBoneSegment[] _boneGuides = [];
    private Vector3[] _jointDots = [];
    private Vector3[] _overlayPolyline = [];
    private Vector3[] _overlaySegments = [];
    private Vector3[] _meshVertices = [];
    private int[] _meshIndices = [];

    public StickViewMode ViewMode { get; set; } = StickViewMode.FrontXy;

    public string Caption { get; set; } = "";

    /// <summary>
    /// When set, camera framing stays locked (world U/V extents) so a collapsing ragdoll
    /// does not zoom/chase the pile.
    /// </summary>
    public (float MinU, float MaxU, float MinV, float MaxV)? FixedViewBounds { get; set; }

    public void SetMannequin(MannequinCapsule[] limbs, Vector3 headCenter)
    {
        _limbs = limbs;
        _head = headCenter;
        InvalidateVisual();
    }

    /// <summary>Single adaptive person mesh (filled projected triangles).</summary>
    public void SetAdaptiveMesh(IReadOnlyList<Vector3> vertices, IReadOnlyList<int> indices)
    {
        _meshVertices = vertices.Count == 0 ? [] : vertices.ToArray();
        _meshIndices = indices.Count == 0 ? [] : indices.ToArray();
        InvalidateVisual();
    }

    public void ClearAdaptiveMesh()
    {
        _meshVertices = [];
        _meshIndices = [];
    }

    public void SetBoneGuides(HumanoidBoneSegment[] segments)
    {
        _boneGuides = segments;
        InvalidateVisual();
    }

    public void SetJointDots(IReadOnlyList<Vector3> centers)
    {
        _jointDots = centers.Count == 0 ? [] : centers.ToArray();
        InvalidateVisual();
    }

    public void SetOverlayPolyline(params Vector3[] points)
    {
        _overlayPolyline = points;
        InvalidateVisual();
    }

    public void SetOverlaySegments(params Vector3[] pairedEnds)
    {
        _overlaySegments = pairedEnds;
        InvalidateVisual();
    }

    public void ClearExtras()
    {
        _boneGuides = [];
        _jointDots = [];
        _overlayPolyline = [];
        _overlaySegments = [];
        ClearAdaptiveMesh();
    }

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        context.FillRectangle(LabPalette.PaneBrush, new Rect(bounds.Size));
        context.DrawRectangle(
            null,
            new Pen(LabPalette.PaneEdgeBrush, 1.5),
            new Rect(0.75, 0.75, bounds.Width - 1.5, bounds.Height - 1.5));

        if (!TryBuildProjection(bounds.Size, out var originX, out var originY, out var pixelsPerMeter))
        {
            DrawCaption(context, bounds);
            return;
        }

        DrawGround(context, originX, originY, pixelsPerMeter, bounds.Width);

        // Adaptive person mesh (single surface following the ragdoll).
        if (_meshIndices.Length >= 3 && _meshVertices.Length > 0)
        {
            var fill = new SolidColorBrush(Color.FromArgb(170, 196, 122, 58));
            var edge = new Pen(new SolidColorBrush(Color.FromArgb(200, 42, 168, 168)), 0.8);
            for (var i = 0; i + 2 < _meshIndices.Length; i += 3)
            {
                var a = Project(_meshVertices[_meshIndices[i]], originX, originY, pixelsPerMeter);
                var b = Project(_meshVertices[_meshIndices[i + 1]], originX, originY, pixelsPerMeter);
                var c = Project(_meshVertices[_meshIndices[i + 2]], originX, originY, pixelsPerMeter);
                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    ctx.BeginFigure(a, isFilled: true);
                    ctx.LineTo(b);
                    ctx.LineTo(c);
                    ctx.EndFigure(true);
                }

                context.DrawGeometry(fill, edge, geo);
            }
        }

        // Optional capsule mannequin underlay (thin) when no adaptive mesh.
        if (_meshIndices.Length == 0)
        {
            foreach (var limb in _limbs)
            {
                var a = Project(limb.A, originX, originY, pixelsPerMeter);
                var b = Project(limb.B, originX, originY, pixelsPerMeter);
                var thickness = Math.Max(4.0, limb.RadiusMeters * 2.0 * pixelsPerMeter);
                var edgePen = new Pen(LabPalette.TealBrush, thickness + 2.5, lineCap: PenLineCap.Round);
                var fillPen = new Pen(LabPalette.CopperBrush, thickness, lineCap: PenLineCap.Round);
                context.DrawLine(edgePen, a, b);
                context.DrawLine(fillPen, a, b);
            }

            if (_head is { } head)
            {
                var p = Project(head, originX, originY, pixelsPerMeter);
                var r = Math.Max(6.0, 0.14 * pixelsPerMeter);
                context.DrawEllipse(LabPalette.AmberBrush, new Pen(LabPalette.TealBrush, 1.5), p, r, r);
            }
        }

        var guidePen = new Pen(new SolidColorBrush(Color.FromArgb(140, 42, 168, 168)), 1.2, lineCap: PenLineCap.Round);
        foreach (var seg in _boneGuides)
        {
            var a = Project(seg.Start, originX, originY, pixelsPerMeter);
            var b = Project(seg.End, originX, originY, pixelsPerMeter);
            context.DrawLine(guidePen, a, b);
        }

        foreach (var c in _jointDots)
        {
            var p = Project(c, originX, originY, pixelsPerMeter);
            context.DrawEllipse(LabPalette.TealBrightBrush, null, p, 3.0, 3.0);
        }

        var overlayPen = new Pen(LabPalette.AmberBrush, 2.4, lineCap: PenLineCap.Round);
        var stringPen = new Pen(LabPalette.InkBrush, 1.6, lineCap: PenLineCap.Round);

        if (_overlayPolyline.Length >= 2)
        {
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(Project(_overlayPolyline[0], originX, originY, pixelsPerMeter), false);
                for (var i = 1; i < _overlayPolyline.Length; i++)
                    ctx.LineTo(Project(_overlayPolyline[i], originX, originY, pixelsPerMeter));
            }

            context.DrawGeometry(null, overlayPen, geo);
        }

        for (var i = 0; i + 1 < _overlaySegments.Length; i += 2)
        {
            var a = Project(_overlaySegments[i], originX, originY, pixelsPerMeter);
            var b = Project(_overlaySegments[i + 1], originX, originY, pixelsPerMeter);
            context.DrawLine(stringPen, a, b);
        }

        DrawCaption(context, bounds);
    }

    private bool TryBuildProjection(Size size, out double originX, out double originY, out double pixelsPerMeter)
    {
        originX = size.Width * 0.5;
        originY = size.Height * 0.82;
        pixelsPerMeter = 1;

        float minU, maxU, minV, maxV;
        if (FixedViewBounds is { } fixedBounds)
        {
            minU = fixedBounds.MinU;
            maxU = fixedBounds.MaxU;
            minV = fixedBounds.MinV;
            maxV = fixedBounds.MaxV;
        }
        else
        {
            var points = CollectPoints();
            if (points.Count == 0)
                return false;

            minU = float.MaxValue;
            maxU = float.MinValue;
            minV = float.MaxValue;
            maxV = float.MinValue;
            foreach (var p in points)
            {
                Map(p, out var u, out var v);
                minU = Math.Min(minU, u);
                maxU = Math.Max(maxU, u);
                minV = Math.Min(minV, v);
                maxV = Math.Max(maxV, v);
            }
        }

        var spanU = Math.Max(0.5f, maxU - minU);
        var spanV = Math.Max(1.2f, maxV - minV);
        const double pad = 0.12;
        var scaleU = (size.Width * (1.0 - 2 * pad)) / spanU;
        var scaleV = (size.Height * (1.0 - 2 * pad)) / spanV;
        pixelsPerMeter = Math.Min(scaleU, scaleV);

        var midU = (minU + maxU) * 0.5;
        var midV = (minV + maxV) * 0.5;
        originX = size.Width * 0.5 - midU * pixelsPerMeter;
        originY = size.Height * 0.5 + midV * pixelsPerMeter;
        return true;
    }

    private List<Vector3> CollectPoints()
    {
        var list = new List<Vector3>(64);
        foreach (var limb in _limbs)
        {
            list.Add(limb.A);
            list.Add(limb.B);
        }

        if (_head is { } h)
            list.Add(h);
        foreach (var s in _boneGuides)
        {
            list.Add(s.Start);
            list.Add(s.End);
        }

        list.AddRange(_jointDots);
        list.AddRange(_overlayPolyline);
        list.AddRange(_overlaySegments);
        list.AddRange(_meshVertices);
        return list;
    }

    private void Map(Vector3 world, out float u, out float v)
    {
        if (ViewMode == StickViewMode.SideZy)
        {
            u = world.Z;
            v = world.Y;
        }
        else
        {
            u = world.X;
            v = world.Y;
        }
    }

    private Point Project(Vector3 world, double originX, double originY, double ppm)
    {
        Map(world, out var u, out var v);
        return new Point(originX + u * ppm, originY - v * ppm);
    }

    private void DrawCaption(DrawingContext context, Rect bounds)
    {
        if (string.IsNullOrEmpty(Caption))
            return;

        var text = new FormattedText(
            Caption,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            13,
            LabPalette.InkBrush);
        context.DrawText(text, new Point(12, 10));
    }

    private void DrawGround(
        DrawingContext context,
        double originX,
        double originY,
        double ppm,
        double width)
    {
        // World Y = 0 projected into screen space.
        var y = originY - 0.0 * ppm;
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(160, 26, 107, 107)), 1.5);
        context.DrawLine(pen, new Point(10, y), new Point(width - 10, y));
        _ = originX;
    }
}
