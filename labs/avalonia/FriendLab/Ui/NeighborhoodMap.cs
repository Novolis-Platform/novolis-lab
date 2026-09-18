using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using FriendLab.Core;

namespace FriendLab.Ui;

/// <summary>Simple projected pin map for the Harbor District demo cluster.</summary>
internal sealed class NeighborhoodMap : Control
{
    public static readonly StyledProperty<IReadOnlyList<FriendProfile>?> ProfilesProperty =
        AvaloniaProperty.Register<NeighborhoodMap, IReadOnlyList<FriendProfile>?>(nameof(Profiles));

    public static readonly StyledProperty<string?> HighlightIdProperty =
        AvaloniaProperty.Register<NeighborhoodMap, string?>(nameof(HighlightId));

    public static readonly StyledProperty<string?> EditableIdProperty =
        AvaloniaProperty.Register<NeighborhoodMap, string?>(nameof(EditableId));

    public IReadOnlyList<FriendProfile>? Profiles
    {
        get => GetValue(ProfilesProperty);
        set => SetValue(ProfilesProperty, value);
    }

    public string? HighlightId
    {
        get => GetValue(HighlightIdProperty);
        set => SetValue(HighlightIdProperty, value);
    }

    public string? EditableId
    {
        get => GetValue(EditableIdProperty);
        set => SetValue(EditableIdProperty, value);
    }

    public event Action<string, double, double>? LocationDragged;

    Point? _dragStart;
    string? _dragId;

    static NeighborhoodMap()
    {
        AffectsRender<NeighborhoodMap>(ProfilesProperty, HighlightIdProperty, EditableIdProperty);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (EditableId is null || Profiles is null)
            return;

        var pt = e.GetPosition(this);
        foreach (var profile in Profiles)
        {
            if (profile.Id != EditableId)
                continue;
            var pin = Project(profile.Latitude, profile.Longitude);
            if (Distance(pt, pin) <= 14)
            {
                _dragId = profile.Id;
                _dragStart = pt;
                e.Pointer.Capture(this);
                e.Handled = true;
                return;
            }
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragId is null || _dragStart is null || Profiles is null)
            return;

        var pt = e.GetPosition(this);
        var (lat, lon) = Unproject(pt);
        LocationDragged?.Invoke(_dragId, lat, lon);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragId is not null)
        {
            e.Pointer.Capture(null);
            _dragId = null;
            _dragStart = null;
            e.Handled = true;
        }
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(FriendPalette.MistBrush, bounds);

        // soft atmosphere bands
        context.FillRectangle(
            new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                [
                    new GradientStop(Color.Parse("#c5d9ce"), 0),
                    new GradientStop(Color.Parse("#e8f0eb"), 0.55),
                    new GradientStop(Color.Parse("#d0e0d6"), 1),
                ],
            },
            bounds);

        DrawGrid(context, bounds);

        var profiles = Profiles;
        if (profiles is null || profiles.Count == 0)
            return;

        foreach (var profile in profiles)
        {
            var pin = Project(profile.Latitude, profile.Longitude);
            var accent = Color.Parse(profile.AccentHex);
            var isHighlight = string.Equals(profile.Id, HighlightId, StringComparison.Ordinal);
            var radiusPx = KmToPixels(profile.RadiusKm);

            if (isHighlight || string.Equals(profile.Id, EditableId, StringComparison.Ordinal))
            {
                context.DrawEllipse(
                    new SolidColorBrush(Color.FromArgb(40, accent.R, accent.G, accent.B)),
                    new Pen(new SolidColorBrush(Color.FromArgb(120, accent.R, accent.G, accent.B)), 1.2),
                    pin,
                    radiusPx,
                    radiusPx);
            }

            var r = isHighlight ? 8.0 : 6.0;
            context.DrawEllipse(
                new SolidColorBrush(accent),
                new Pen(FriendPalette.PineDeepBrush, isHighlight ? 2 : 1),
                pin,
                r,
                r);

            var label = new FormattedText(
                profile.DisplayName.Split(' ')[0],
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(FriendPalette.Body),
                11,
                FriendPalette.InkBrush);
            context.DrawText(label, new Point(pin.X + 10, pin.Y - 8));
        }
    }

    void DrawGrid(DrawingContext context, Rect bounds)
    {
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(50, 30, 58, 47)), 1);
        const int step = 36;
        for (var x = 0.0; x < bounds.Width; x += step)
            context.DrawLine(pen, new Point(x, 0), new Point(x, bounds.Height));
        for (var y = 0.0; y < bounds.Height; y += step)
            context.DrawLine(pen, new Point(0, y), new Point(bounds.Width, y));
    }

    Point Project(double lat, double lon)
    {
        // Local tangent plane around demo origin, padded
        var eastKm = (lon - DemoSeed.OriginLon) * 111.0 * Math.Cos(DemoSeed.OriginLat * Math.PI / 180.0);
        var northKm = (lat - DemoSeed.OriginLat) * 111.0;
        var scale = Math.Min(Bounds.Width, Bounds.Height) / 12.0; // ~12 km viewport
        var cx = Bounds.Width / 2;
        var cy = Bounds.Height / 2;
        return new Point(cx + eastKm * scale, cy - northKm * scale);
    }

    (double Lat, double Lon) Unproject(Point pt)
    {
        var scale = Math.Min(Bounds.Width, Bounds.Height) / 12.0;
        if (scale < 0.001)
            return (DemoSeed.OriginLat, DemoSeed.OriginLon);
        var eastKm = (pt.X - Bounds.Width / 2) / scale;
        var northKm = (Bounds.Height / 2 - pt.Y) / scale;
        var lat = DemoSeed.OriginLat + northKm / 111.0;
        var lon = DemoSeed.OriginLon + eastKm / (111.0 * Math.Cos(DemoSeed.OriginLat * Math.PI / 180.0));
        return (lat, lon);
    }

    double KmToPixels(double km)
    {
        var scale = Math.Min(Bounds.Width, Bounds.Height) / 12.0;
        return Math.Max(8, km * scale);
    }

    static double Distance(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
