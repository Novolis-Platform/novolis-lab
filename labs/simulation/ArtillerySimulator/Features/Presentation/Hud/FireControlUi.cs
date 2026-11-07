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
        var fire = false;
        var snapGun = false;
        var snapBattle = false;
        var map = false;
        var toggleFollow = false;

        if (!frame.IsMouseButtonPressed(MouseButton.Left))
            return new FrameActions(null, false, false, false, false, false);

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

        return new FrameActions(charge, fire, snapGun, snapBattle, map, toggleFollow);
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
        string? defenseLine,
        string? fireBlockReason,
        bool solutionUnsafe,
        bool launchDetonation)
    {
        _width = frame.Width;
        _height = frame.Height;
        _scale = Math.Clamp(Math.Min(_width, _height) / 900f, 0.85f, 1.4f);
        EnsureSurface(_width, _height);

        var sampleAlt = shot.Phase == BallisticTrajectoryPhase.InFlight
            ? shot.CurrentPosition.Y
            : terrain.GunBaseline.Y;
        var windSpeed = atmosphere.WindSpeedMetersPerSecond;
        var windFrom = atmosphere.WindFromDegrees;
        var tempC = atmosphere.ReferenceTemperatureKelvin - 273.15;
        var qnh = atmosphere.ReferencePressurePa / 100.0;
        var rh = atmosphere.RelativeHumidity * 100.0;

        var orderedElevMils = orderedElevation * 6400f / 360f;
        var orderedDeflMils = (orderedAzimuth - gun.CenterAzimuthDegrees) * 6400f / 360f;
        var heroRange = shot.Phase switch
        {
            BallisticTrajectoryPhase.InFlight => Horiz(terrain.GunBaseline, shot.CurrentPosition),
            BallisticTrajectoryPhase.Impacted when shot.Impact is { } hit => hit.HorizontalRangeMeters,
            _ => predictedRangeMeters,
        };
        // Fire when breech is ready and the shell clears the piece (HE min is warning-only).
        var canFire = fireBlockReason is null;

        // Rebuild hit boxes every frame (cheap); rasterize Skia only when chrome meaningfully changes.
        _buttons.Clear();
        BuildButtons(gun, followProjectile, canFire);

        var signature = string.Create(
            CultureInfo.InvariantCulture,
            $"{_width}x{_height}|{gun.ElevationMils:F0}|{orderedElevMils:F0}|{gun.DeflectionMils:F0}|{orderedDeflMils:F0}|{predictedRangeMeters:F0}|{predictedTofSeconds:F1}|{heroRange:F0}|{gun.ChargeIndex}|{followProjectile}|{_chargeMenuOpen}|{canFire}|{defenseLine}|{fireBlockReason}|{shot.Phase}|{solutionUnsafe}|{launchDetonation}|{windSpeed:F1}|{windFrom:F0}|{tempC:F0}|{qnh:F0}|{rh:F0}|{predictedImpact.Y:F0}");
        _rasterAge += frame.DeltaSeconds;
        var dirty = signature != _lastSignature || _rasterAge >= 0.12f;
        if (dirty)
        {
            _lastSignature = signature;
            _rasterAge = 0f;
            var canvas = _canvas!;
            canvas.Clear(SKColors.Transparent);
            DrawTopBar(canvas, shot, heroRange, gun, defenseLine, fireBlockReason, launchDetonation, solutionUnsafe);
            DrawGunPanel(
                canvas,
                gun,
                orderedElevMils,
                orderedDeflMils,
                fireBlockReason,
                solutionUnsafe,
                launchDetonation,
                predictedRangeMeters,
                predictedTofSeconds);
            DrawMetPanel(canvas, atmosphere, sampleAlt, windSpeed, windFrom, tempC, qnh, rh, predictedImpact, terrain);
            DrawChargeMenu(canvas, gun);
            DrawCameraRow(canvas, followProjectile);
            DrawFireButton(canvas, canFire);
            DrawLegend(canvas, solutionUnsafe);
            DrawHelp(canvas);
            CopyPixels();
        }

        frame.SetTitle(string.Create(
            CultureInfo.InvariantCulture,
            $"{ProductIdentity.Title}  |  {SimulationUnits.FormatRange(heroRange)}  |  {gun.ChargeLabel}  |  {(followProjectile ? "FOLLOW SHELL" : "FOLLOW GUN")}  |  {fps:F0} fps"));

        frame.ClearDepth();
        frame.BlitOverlay(_pixels, _width, _height);
        LastOverlayLineCount = 0;
    }

    private void BuildButtons(GunModel gun, bool followProjectile, bool canFire)
    {
        var bw = 280f * _scale;
        var bh = 42f * _scale;
        var bx = 8f;
        var by = _height - 180f * _scale;
        AddButton("charge-toggle", bx, by, bw, bh, $"CHARGE  {gun.ChargeLabel.ToUpperInvariant()}", selected: true);
        if (_chargeMenuOpen)
        {
            for (var i = 0; i < gun.Spec.Charges.Count; i++)
            {
                var y = by - (i + 1) * (bh + 4 * _scale);
                AddButton($"charge-{i}", bx, y, bw, bh, gun.Spec.Charges[i].Label, gun.ChargeIndex == i);
            }
        }

        var gap = 8f * _scale;
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
        string? fireBlockReason,
        bool launchDetonation,
        bool solutionUnsafe)
    {
        var topH = 72f * _scale;
        PanelBox(canvas, 8, 8, _width - 16, topH);

        var phase = launchDetonation ? "NO CLR"
            : solutionUnsafe ? "UNSAFE"
            : fireBlockReason is not null ? "BUSY"
            : shot.Phase == BallisticTrajectoryPhase.Impacted ? "IMPACT"
            : "READY";
        var phaseColor = launchDetonation || solutionUnsafe || fireBlockReason is not null ? Alert
            : shot.Phase == BallisticTrajectoryPhase.Impacted ? Alert
            : Ready;

        DrawText(canvas, phase, 20 * _scale, 28 * _scale, 22f * _scale, phaseColor);
        DrawText(
            canvas,
            $"{SimulationUnits.FormatRange(heroRange)}   ZONE {gun.ChargeLabel.ToUpperInvariant()}",
            200 * _scale,
            28 * _scale,
            20f * _scale,
            Text);

        var mission = string.IsNullOrEmpty(defenseLine) ? ProductIdentity.Subtitle.ToUpperInvariant() : defenseLine;
        DrawText(canvas, Truncate(mission, 72), 20 * _scale, 56 * _scale, 15f * _scale, Accent);
    }

    private void DrawGunPanel(
        SKCanvas canvas,
        GunModel gun,
        float orderedElevMils,
        float orderedDeflMils,
        string? fireBlockReason,
        bool solutionUnsafe,
        bool launchDetonation,
        float predictedRange,
        float tof)
    {
        var px = 8f;
        var py = 92f * _scale;
        var pw = 360f * _scale;
        var ph = 230f * _scale;
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
        DrawText(canvas, $"PRED  {SimulationUnits.FormatRange(predictedRange)}   TOF {tof:F1} S", lx, ly, ls, solutionUnsafe ? Alert : Text);
        ly += lh;
        if (launchDetonation)
        {
            DrawText(canvas, "NO CLEARANCE — DETONATION AT PIECE", lx, ly, ls, Alert);
            ly += lh;
        }
        else if (solutionUnsafe)
        {
            DrawText(canvas, $"BELOW HE MIN {SimulationUnits.MinEffectiveRangeMeters / 1000f:F1} KM", lx, ly, ls, Alert);
            ly += lh;
        }

        DrawText(
            canvas,
            fireBlockReason is null ? "CREW READY" : Truncate(fireBlockReason.ToUpperInvariant(), 36),
            lx,
            ly,
            ls,
            fireBlockReason is null ? Ready : Alert);
    }

    private void DrawMetPanel(
        SKCanvas canvas,
        AtmosphereModel atmosphere,
        float sampleAlt,
        float windSpeed,
        float windFrom,
        double tempC,
        double qnh,
        double rh,
        Vector3 predictedImpact,
        TerrainWorld terrain)
    {
        var pw = 360f * _scale;
        var ph = 210f * _scale;
        var px = _width - pw - 8;
        var py = 92f * _scale;
        PanelBox(canvas, px, py, pw, ph);

        var lx = px + 16 * _scale;
        var ly = py + 28 * _scale;
        var ls = 15f * _scale;
        var lh = 26f * _scale;
        var rho = atmosphere.DensityKgPerM3At(sampleAlt);

        DrawText(canvas, "MET / MPI", lx, ly, 18f * _scale, Accent);
        ly += lh;
        DrawText(canvas, string.Create(CultureInfo.InvariantCulture, $"AIR   {rho:F3} KG/M3  {tempC:F0} C"), lx, ly, ls, Text);
        ly += lh;
        DrawText(canvas, string.Create(CultureInfo.InvariantCulture, $"QNH   {qnh:F0}  RH {rh:F0}%"), lx, ly, ls, Text);
        ly += lh;
        DrawText(canvas, string.Create(CultureInfo.InvariantCulture, $"WIND  {windSpeed:F1} M/S  FROM {windFrom:F0}"), lx, ly, ls, Text);
        ly += lh;
        DrawText(canvas, $"LAT   {KristiansandHeightSampler.MeanLatitudeDegrees:F1} N  KRS COAST", lx, ly, ls, Text);
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

        for (var i = 0; i < gun.Spec.Charges.Count; i++)
        {
            var y = by - (i + 1) * (bh + 4 * _scale);
            var zone = gun.Spec.Charges[i];
            var item = $"{i + 1}  {zone.Label.ToUpperInvariant()}  {zone.MuzzleSpeedMps:F0} M/S";
            DrawButtonVisual(canvas, bx, y, bw, bh, item, gun.ChargeIndex == i);
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

    private void DrawLegend(SKCanvas canvas, bool solutionUnsafe)
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
        var aim = solutionUnsafe ? BallisticArcColors.OrderedUnsafe : BallisticArcColors.Ordered;
        var prob = solutionUnsafe ? BallisticArcColors.ProbabilityUnsafe : BallisticArcColors.Probability;
        LegendRow(canvas, ref ly, lx, ls, lh, aim, solutionUnsafe ? "AIMED (UNSAFE)" : "AIMED TRAJECTORY");
        LegendRow(canvas, ref ly, lx, ls, lh, prob, solutionUnsafe ? "PROBABILITY (UNSAFE)" : "PROBABILITY CONE");
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
        var help = "WASD LAY  SPACE FIRE  ESC/P PAUSE  L LICENSE  F SHELL CAM  F1 PROFILE";
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
