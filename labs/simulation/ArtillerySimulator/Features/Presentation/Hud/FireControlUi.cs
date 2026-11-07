using System.Globalization;
using System.Numerics;
using Novolis.Math.Geometry;
using Novolis.Physics.Ballistics;
using Novolis.Silk;
using SkiaSharp;

namespace ArtillerySimulator;

/// <summary>
/// Mouse-driven fire-control chrome rasterized with SkiaSharp fonts, then alpha-blitted over the 3D frame.
/// Keyboard aims (WASD / arrows) and fires (Space); mouse never lays the tube.
/// </summary>
internal sealed class FireControlUi : IDisposable
{
    private static readonly SKColor PanelFill = new(36, 42, 52, 220);
    private static readonly SKColor PanelHotFill = new(52, 40, 36, 230);
    private static readonly SKColor Border = new(200, 220, 235, 255);
    private static readonly SKColor Text = new(245, 248, 252, 255);
    private static readonly SKColor Accent = new(255, 210, 110, 255);
    private static readonly SKColor Ready = new(140, 240, 160, 255);
    private static readonly SKColor Alert = new(255, 150, 90, 255);
    private static readonly SKColor Dim = new(190, 200, 210, 220);
    private static readonly SKColor Selected = new(120, 230, 255, 255);

    private readonly List<UiButton> _buttons = [];
    private readonly SKTypeface _typeface;
    private SKBitmap? _bitmap;
    private SKCanvas? _canvas;
    private Rgba32[] _pixels = [];
    private bool _chargeMenuOpen;
    private float _scale = 1f;
    private int _width;
    private int _height;
    private string _lastSignature = "";
    private float _rasterAge = 99f;

    public FireControlUi()
    {
        _typeface =
            SKTypeface.FromFamilyName("Cascadia Mono", SKFontStyle.Bold)
            ?? SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold)
            ?? SKTypeface.FromFamilyName("Courier New", SKFontStyle.Bold)
            ?? SKTypeface.Default;
    }

    public static int LastOverlayLineCount { get; private set; }

    public bool PointerOverUi { get; private set; }

    public readonly record struct FrameActions(
        int? ChargeIndex,
        int? PaceIndex,
        bool Fire,
        bool SnapGun,
        bool SnapBattle,
        bool MapOverview,
        bool ToggleFollow);

    public FrameActions HandleMouse(SilkFrame frame)
    {
        var mx = frame.MouseX;
        var my = frame.MouseY;
        PointerOverUi = HitAny(mx, my);

        int? charge = null;
        int? pace = null;
        var fire = false;
        var snapGun = false;
        var snapBattle = false;
        var map = false;
        var toggleFollow = false;

        if (!frame.IsMouseButtonPressed(MouseButton.Left))
            return new FrameActions(null, null, false, false, false, false, false);

        foreach (var btn in _buttons)
        {
            if (!btn.Contains(mx, my))
                continue;

            switch (btn.Id)
            {
                case "charge-toggle":
                    _chargeMenuOpen = !_chargeMenuOpen;
                    break;
                case "charge-0":
                case "charge-1":
                case "charge-2":
                case "charge-3":
                    charge = btn.Id[^1] - '0';
                    _chargeMenuOpen = false;
                    break;
                case "pace-0":
                case "pace-1":
                case "pace-2":
                case "pace-3":
                    pace = btn.Id[^1] - '0';
                    break;
                case "fire":
                    fire = true;
                    break;
                case "snap-gun":
                    snapGun = true;
                    break;
                case "snap-battle":
                    snapBattle = true;
                    break;
                case "map":
                    map = true;
                    break;
                case "follow":
                    toggleFollow = true;
                    break;
            }

            break;
        }

        return new FrameActions(charge, pace, fire, snapGun, snapBattle, map, toggleFollow);
    }

    public void Draw(
        SilkFrame frame,
        GunModel gun,
        float orderedElevation,
        float orderedAzimuth,
        TerrainWorld terrain,
        AtmosphereModel atmosphere,
        BallisticTrajectoryRunner shot,
        bool followProjectile,
        float fps,
        float predictedRangeMeters,
        float predictedTofSeconds,
        Vector3 predictedImpact,
        int paceIndex,
        string paceLabel,
        string? defenseLine,
        string? reloadLine)
    {
        _width = frame.Width;
        _height = frame.Height;
        _scale = Math.Clamp(Math.Min(_width, _height) / 900f, 0.85f, 1.4f);
        EnsureSurface(_width, _height);

        var sampleAlt = shot.Phase == BallisticTrajectoryPhase.InFlight
            ? shot.CurrentPosition.Y
            : terrain.GunBaseline.Y;
        var wind = atmosphere.SurfaceWindMetersPerSecond;
        var windSpeed = wind.Length();
        var windBearing = MathF.Atan2(wind.Z, wind.X) * (180f / MathF.PI);
        if (windBearing < 0f)
            windBearing += 360f;

        var orderedElevMils = orderedElevation * 6400f / 360f;
        var orderedDeflMils = (orderedAzimuth - gun.CenterAzimuthDegrees) * 6400f / 360f;
        var heroRange = shot.Phase switch
        {
            BallisticTrajectoryPhase.InFlight => Horiz(terrain.GunBaseline, shot.CurrentPosition),
            BallisticTrajectoryPhase.Impacted when shot.Impact is { } hit => hit.HorizontalRangeMeters,
            _ => predictedRangeMeters,
        };
        var underMin = predictedRangeMeters < SimulationUnits.MinEffectiveRangeMeters;
        // reloadLine is null once the breech is closed — fire snaps the tube; do not wait on lay.
        var canFire = reloadLine is null && shot.Phase != BallisticTrajectoryPhase.InFlight;

        // Rebuild hit boxes every frame (cheap); rasterize Skia only when chrome meaningfully changes.
        _buttons.Clear();
        BuildButtons(gun, paceIndex, followProjectile, canFire);

        var signature = string.Create(
            CultureInfo.InvariantCulture,
            $"{_width}x{_height}|{gun.ElevationMils:F0}|{orderedElevMils:F0}|{gun.DeflectionMils:F0}|{orderedDeflMils:F0}|{predictedRangeMeters:F0}|{predictedTofSeconds:F1}|{heroRange:F0}|{gun.ChargeIndex}|{paceIndex}|{followProjectile}|{_chargeMenuOpen}|{canFire}|{defenseLine}|{reloadLine}|{shot.Phase}|{underMin}|{windSpeed:F0}|{windBearing:F0}|{predictedImpact.Y:F0}");
        _rasterAge += frame.DeltaSeconds;
        var dirty = signature != _lastSignature || _rasterAge >= 0.12f;
        if (dirty)
        {
            _lastSignature = signature;
            _rasterAge = 0f;
            var canvas = _canvas!;
            canvas.Clear(SKColors.Transparent);
            DrawTopBar(canvas, shot, heroRange, gun, defenseLine, reloadLine);
            DrawGunPanel(canvas, gun, orderedElevMils, orderedDeflMils, reloadLine, underMin, predictedRangeMeters, predictedTofSeconds);
            DrawMetPanel(canvas, atmosphere, sampleAlt, windSpeed, windBearing, predictedImpact, terrain);
            DrawChargeMenu(canvas, gun);
            DrawPaceRow(canvas, paceIndex);
            DrawCameraRow(canvas, followProjectile);
            DrawFireButton(canvas, canFire);
            DrawLegend(canvas);
            DrawHelp(canvas);
            CopyPixels();
        }

        frame.SetTitle(string.Create(
            CultureInfo.InvariantCulture,
            $"Kristiansand K9  |  {SimulationUnits.FormatRange(heroRange)}  |  {gun.ChargeLabel}  |  {(followProjectile ? "FOLLOW SHELL" : "FOLLOW GUN")}  |  {fps:F0} fps"));

        frame.ClearDepth();
        frame.BlitOverlay(_pixels, _width, _height);
        LastOverlayLineCount = 0;
        _ = paceLabel;
    }

    private void BuildButtons(GunModel gun, int paceIndex, bool followProjectile, bool canFire)
    {
        var bw = 280f * _scale;
        var bh = 42f * _scale;
        var bx = 8f;
        var by = _height - 230f * _scale;
        AddButton("charge-toggle", bx, by, bw, bh, $"CHARGE  {gun.ChargeLabel.ToUpperInvariant()}", selected: true);
        if (_chargeMenuOpen)
        {
            for (var i = 0; i < SimulationUnits.ChargeLabels.Length; i++)
            {
                var y = by - (i + 1) * (bh + 4 * _scale);
                AddButton($"charge-{i}", bx, y, bw, bh, SimulationUnits.ChargeLabels[i], gun.ChargeIndex == i);
            }
        }

        var paceLabels = new[] { "1X", "3X", "8X", "24X" };
        var pbw = 72f * _scale;
        var pbh = 36f * _scale;
        var gap = 8f * _scale;
        var total = paceLabels.Length * pbw + (paceLabels.Length - 1) * gap;
        var x0 = (_width - total) * 0.5f;
        var py = _height - 120f * _scale;
        for (var i = 0; i < paceLabels.Length; i++)
            AddButton($"pace-{i}", x0 + i * (pbw + gap), py, pbw, pbh, paceLabels[i], paceIndex == i);

        var cbw = 120f * _scale;
        var cbh = 36f * _scale;
        var cx = _width - 8 - 4 * cbw - 3 * gap;
        var cy = _height - 120f * _scale;
        AddButton("follow", cx, cy, cbw, cbh, followProjectile ? "SHELL" : "GUN", true);
        AddButton("snap-gun", cx + cbw + gap, cy, cbw, cbh, "GUN VIEW", false);
        AddButton("snap-battle", cx + 2 * (cbw + gap), cy, cbw, cbh, "BATTLE", false);
        AddButton("map", cx + 3 * (cbw + gap), cy, cbw, cbh, "MAP", false);

        var fbw = 180f * _scale;
        var fbh = 48f * _scale;
        AddButton("fire", (_width - fbw) * 0.5f, _height - 64f * _scale, fbw, fbh, "FIRE", canFire);
    }

    public void Dispose()
    {
        _canvas?.Dispose();
        _bitmap?.Dispose();
        _typeface.Dispose();
        _canvas = null;
        _bitmap = null;
    }

    private void EnsureSurface(int width, int height)
    {
        if (_bitmap is { } existing && existing.Width == width && existing.Height == height)
            return;

        _canvas?.Dispose();
        _bitmap?.Dispose();
        _bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        _canvas = new SKCanvas(_bitmap);
        _pixels = new Rgba32[width * height];
    }

    private void CopyPixels()
    {
        if (_bitmap is null)
            return;

        // SKColorType.Rgba8888 → byte order matches Novolis.Math.Geometry.Rgba32.
        using var pixmap = _bitmap.PeekPixels();
        var bytes = pixmap.GetPixelSpan<byte>();
        var n = Math.Min(_pixels.Length, bytes.Length / 4);
        for (var i = 0; i < n; i++)
        {
            var o = i * 4;
            _pixels[i] = new Rgba32(bytes[o], bytes[o + 1], bytes[o + 2], bytes[o + 3]);
        }
    }

    private void DrawTopBar(
        SKCanvas canvas,
        BallisticTrajectoryRunner shot,
        float heroRange,
        GunModel gun,
        string? defenseLine,
        string? reloadLine)
    {
        var topH = 72f * _scale;
        PanelBox(canvas, 8, 8, _width - 16, topH);

        var phase = shot.Phase switch
        {
            BallisticTrajectoryPhase.InFlight => "IN FLIGHT",
            BallisticTrajectoryPhase.Impacted => "IMPACT",
            _ => reloadLine is null ? "READY" : "BUSY",
        };
        var phaseColor = shot.Phase == BallisticTrajectoryPhase.InFlight ? Accent
            : shot.Phase == BallisticTrajectoryPhase.Impacted ? Alert
            : reloadLine is null ? Ready : Alert;

        DrawText(canvas, phase, 20 * _scale, 28 * _scale, 22f * _scale, phaseColor);
        DrawText(
            canvas,
            $"{SimulationUnits.FormatRange(heroRange)}   ZONE {gun.ChargeLabel.ToUpperInvariant()}",
            200 * _scale,
            28 * _scale,
            20f * _scale,
            Text);

        var mission = string.IsNullOrEmpty(defenseLine) ? "DEFENSE OF KRISTIANSAND — ODDEROYA K9" : defenseLine;
        DrawText(canvas, Truncate(mission, 72), 20 * _scale, 56 * _scale, 15f * _scale, Accent);
    }

    private void DrawGunPanel(
        SKCanvas canvas,
        GunModel gun,
        float orderedElevMils,
        float orderedDeflMils,
        string? reloadLine,
        bool underMin,
        float predictedRange,
        float tof)
    {
        var px = 8f;
        var py = 92f * _scale;
        var pw = 360f * _scale;
        var ph = 210f * _scale;
        PanelBox(canvas, px, py, pw, ph);

        var lx = px + 16 * _scale;
        var ly = py + 28 * _scale;
        var ls = 16f * _scale;
        var lh = 28f * _scale;

        DrawText(canvas, "FIRE CONTROL", lx, ly, 18f * _scale, Accent);
        ly += lh;
        DrawText(canvas, $"QE    {gun.ElevationMils:F0} -> {orderedElevMils:F0} MIL", lx, ly, ls, Text);
        ly += lh;
        DrawText(canvas, $"DEFL  {gun.DeflectionMils:F0} -> {orderedDeflMils:F0} MIL", lx, ly, ls, Text);
        ly += lh;
        DrawText(canvas, $"PRED  {SimulationUnits.FormatRange(predictedRange)}   TOF {tof:F1} S", lx, ly, ls, underMin ? Alert : Text);
        ly += lh;
        if (underMin)
        {
            DrawText(canvas, $"BELOW HE MIN {SimulationUnits.MinEffectiveRangeMeters / 1000f:F1} KM", lx, ly, ls, Alert);
            ly += lh;
        }

        DrawText(
            canvas,
            reloadLine is null ? "CREW READY" : Truncate(reloadLine.ToUpperInvariant(), 32),
            lx,
            ly,
            ls,
            reloadLine is null ? Ready : Alert);
    }

    private void DrawMetPanel(
        SKCanvas canvas,
        AtmosphereModel atmosphere,
        float sampleAlt,
        float windSpeed,
        float windBearing,
        Vector3 predictedImpact,
        TerrainWorld terrain)
    {
        var pw = 360f * _scale;
        var ph = 170f * _scale;
        var px = _width - pw - 8;
        var py = 92f * _scale;
        PanelBox(canvas, px, py, pw, ph);

        var lx = px + 16 * _scale;
        var ly = py + 28 * _scale;
        var ls = 16f * _scale;
        var lh = 28f * _scale;
        var rho = atmosphere.DensityKgPerM3At(sampleAlt);

        DrawText(canvas, "MET / MPI", lx, ly, 18f * _scale, Accent);
        ly += lh;
        DrawText(canvas, $"AIR   {rho:F3} KG/M3  {atmosphere.TemperatureAt(sampleAlt):F0} K", lx, ly, ls, Text);
        ly += lh;
        DrawText(canvas, $"WIND  {windSpeed:F0} M/S  BRG {windBearing:F0}", lx, ly, ls, Text);
        ly += lh;
        DrawText(canvas, $"LAT   {KristiansandHeightSampler.MeanLatitudeDegrees:F1} N", lx, ly, ls, Text);
        ly += lh;
        var mpiRange = Horiz(terrain.GunBaseline, predictedImpact);
        DrawText(
            canvas,
            $"MPI   {SimulationUnits.FormatRange(mpiRange)}  H {predictedImpact.Y:F0} M",
            lx,
            ly,
            ls,
            ToSk(BallisticArcColors.MpiMark));
    }

    private void DrawChargeMenu(SKCanvas canvas, GunModel gun)
    {
        var bw = 280f * _scale;
        var bh = 42f * _scale;
        var bx = 8f;
        var by = _height - 230f * _scale;
        var label = $"CHARGE  {gun.ChargeLabel.ToUpperInvariant()}";
        DrawButtonVisual(canvas, bx, by, bw, bh, label, selected: true);

        if (!_chargeMenuOpen)
            return;

        for (var i = 0; i < SimulationUnits.ChargeLabels.Length; i++)
        {
            var y = by - (i + 1) * (bh + 4 * _scale);
            var item = $"{i + 1}  {SimulationUnits.ChargeLabels[i].ToUpperInvariant()}  {SimulationUnits.ChargeSpeedsMps[i]:F0} M/S";
            DrawButtonVisual(canvas, bx, y, bw, bh, item, gun.ChargeIndex == i);
        }
    }

    private void DrawPaceRow(SKCanvas canvas, int paceIndex)
    {
        var labels = new[] { "1X", "3X", "8X", "24X" };
        var bw = 72f * _scale;
        var bh = 36f * _scale;
        var gap = 8f * _scale;
        var total = labels.Length * bw + (labels.Length - 1) * gap;
        var x0 = (_width - total) * 0.5f;
        var y = _height - 120f * _scale;
        DrawText(canvas, "PACE", x0, y - 10 * _scale, 13f * _scale, Dim);
        for (var i = 0; i < labels.Length; i++)
        {
            var x = x0 + i * (bw + gap);
            DrawButtonVisual(canvas, x, y, bw, bh, labels[i], paceIndex == i);
        }
    }

    private void DrawCameraRow(SKCanvas canvas, bool followProjectile)
    {
        var bw = 120f * _scale;
        var bh = 36f * _scale;
        var gap = 8f * _scale;
        var x = _width - 8 - 4 * bw - 3 * gap;
        var y = _height - 120f * _scale;

        void Cam(string label, bool on)
        {
            DrawButtonVisual(canvas, x, y, bw, bh, label, on);
            x += bw + gap;
        }

        Cam(followProjectile ? "SHELL" : "GUN", true);
        Cam("GUN VIEW", false);
        Cam("BATTLE", false);
        Cam("MAP", false);
    }

    private void DrawFireButton(SKCanvas canvas, bool canFire)
    {
        var bw = 180f * _scale;
        var bh = 48f * _scale;
        var bx = (_width - bw) * 0.5f;
        var by = _height - 64f * _scale;
        PanelBox(canvas, bx, by, bw, bh, hot: canFire);
        using (var stroke = new SKPaint
        {
            Color = canFire ? Alert : Dim,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f * _scale,
            IsAntialias = true,
        })
        {
            canvas.DrawRoundRect(bx, by, bw, bh, 4 * _scale, 4 * _scale, stroke);
        }

        DrawCenteredText(canvas, "FIRE", bx, by, bw, bh, 22f * _scale, canFire ? Alert : Dim);
    }

    private void DrawLegend(SKCanvas canvas)
    {
        var pw = 280f * _scale;
        var ph = 190f * _scale;
        var px = _width - pw - 8;
        var py = 280f * _scale;
        PanelBox(canvas, px, py, pw, ph);

        var lx = px + 16 * _scale;
        var ly = py + 28 * _scale;
        var ls = 15f * _scale;
        var lh = 28f * _scale;
        DrawText(canvas, "ARC LEGEND", lx, ly, 17f * _scale, Accent);
        ly += lh;
        LegendRow(canvas, ref ly, lx, ls, lh, BallisticArcColors.Ordered, "AIMED TRAJECTORY");
        LegendRow(canvas, ref ly, lx, ls, lh, BallisticArcColors.Probability, "PROBABILITY CONE");
        LegendRow(canvas, ref ly, lx, ls, lh, BallisticArcColors.Tube, "TUBE (LAYING)");
        LegendRow(canvas, ref ly, lx, ls, lh, BallisticArcColors.Flight, "LIVE ROUND");
        LegendRow(canvas, ref ly, lx, ls, lh, BallisticArcColors.MinRange, "HE MIN RING");
    }

    private void LegendRow(SKCanvas canvas, ref float ly, float lx, float ls, float lh, System.Drawing.Color color, string label)
    {
        var swatch = 22f * _scale;
        var sk = ToSk(color);
        using (var fill = new SKPaint { Color = sk, Style = SKPaintStyle.Fill, IsAntialias = true })
            canvas.DrawRect(lx, ly - 12 * _scale, swatch, 10 * _scale, fill);
        DrawText(canvas, label, lx + swatch + 10 * _scale, ly, ls, Text);
        ly += lh;
    }

    private void DrawHelp(SKCanvas canvas)
    {
        var help = "WASD LAY  SPACE FIRE  F SHELL CAM (IN FLIGHT)  F1 PROFILE  CTRL+C DUMP";
        var size = 13f * _scale;
        using var font = new SKFont(_typeface, size);
        var width = font.MeasureText(help);
        DrawText(canvas, help, Math.Max(12f, (_width - width) * 0.5f), _height - 12f * _scale, size, Dim);
    }

    private void PanelBox(SKCanvas canvas, float x, float y, float w, float h, bool hot = false)
    {
        using var fill = new SKPaint
        {
            Color = hot ? PanelHotFill : PanelFill,
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };
        canvas.DrawRoundRect(x, y, w, h, 6 * _scale, 6 * _scale, fill);

        using var stroke = new SKPaint
        {
            Color = Border,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f * _scale,
            IsAntialias = true,
        };
        canvas.DrawRoundRect(x, y, w, h, 6 * _scale, 6 * _scale, stroke);
    }

    private void AddButton(string id, float x, float y, float w, float h, string label, bool selected)
        => _buttons.Add(new UiButton(id, x, y, w, h, label, selected));

    private void DrawButtonVisual(SKCanvas canvas, float x, float y, float w, float h, string label, bool selected)
    {
        PanelBox(canvas, x, y, w, h, hot: selected);
        if (selected)
        {
            using var stroke = new SKPaint
            {
                Color = Selected,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2f * _scale,
                IsAntialias = true,
            };
            canvas.DrawRoundRect(x, y, w, h, 6 * _scale, 6 * _scale, stroke);
        }

        DrawCenteredText(canvas, label, x, y, w, h, 14f * _scale, selected ? Selected : Text);
    }

    private void DrawText(SKCanvas canvas, string text, float x, float baselineY, float size, SKColor color)
    {
        using var font = new SKFont(_typeface, size);
        using var paint = new SKPaint
        {
            Color = color,
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
        };
        canvas.DrawText(text, x, baselineY, SKTextAlign.Left, font, paint);
    }

    private void DrawCenteredText(SKCanvas canvas, string text, float x, float y, float w, float h, float size, SKColor color)
    {
        using var font = new SKFont(_typeface, size);
        using var paint = new SKPaint
        {
            Color = color,
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
        };
        var metrics = font.Metrics;
        var textWidth = font.MeasureText(text);
        var baseline = y + (h + metrics.CapHeight) * 0.5f;
        canvas.DrawText(text, x + (w - textWidth) * 0.5f, baseline, SKTextAlign.Left, font, paint);
    }

    private bool HitAny(float mx, float my)
    {
        foreach (var btn in _buttons)
        {
            if (btn.Contains(mx, my))
                return true;
        }

        if (mx < 380 * _scale && my < 320 * _scale)
            return true;
        if (mx > _width - 380 * _scale && my < 460 * _scale)
            return true;
        if (my > _height - 250 * _scale)
            return true;
        return false;
    }

    private static float Horiz(Vector3 a, Vector3 b)
    {
        var dx = b.X - a.X;
        var dz = b.Z - a.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    private static string Truncate(string text, int max)
        => text.Length <= max ? text : text[..Math.Max(0, max - 3)] + "...";

    private static SKColor ToSk(System.Drawing.Color c) => new(c.R, c.G, c.B, c.A);

    private readonly record struct UiButton(string Id, float X, float Y, float W, float H, string Label, bool Selected)
    {
        public bool Contains(float mx, float my) =>
            mx >= X && my >= Y && mx <= X + W && my <= Y + H;
    }
}
