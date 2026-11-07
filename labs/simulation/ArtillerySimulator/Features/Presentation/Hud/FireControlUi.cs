using System.Drawing;
using System.Globalization;
using System.Numerics;
using Novolis.Math.Geometry;
using Novolis.Physics.Ballistics;
using Novolis.Silk;

namespace ArtillerySimulator;

/// <summary>
/// Mouse-driven fire-control chrome: charge menu, pace, camera snaps, data readout, arc legend.
/// Keyboard aims (WASD / arrows) and fires (Space); mouse never lays the tube.
/// </summary>
internal sealed class FireControlUi
{
    // Mid-gray translucent — dark enough to kill terrain, light enough that white stick text pops.
    private static readonly Rgba32 PanelRgba = new(36, 42, 52, 210);
    private static readonly Rgba32 PanelHotRgba = new(52, 40, 36, 220);
    private static readonly Color Border = Color.FromArgb(255, 200, 220, 235);
    private static readonly Color Text = Color.FromArgb(255, 255, 255, 255);
    private static readonly Color Accent = Color.FromArgb(255, 255, 210, 110);
    private static readonly Color Ready = Color.FromArgb(255, 140, 240, 160);
    private static readonly Color Alert = Color.FromArgb(255, 255, 150, 90);
    private static readonly Color Dim = Color.FromArgb(255, 190, 200, 210);
    private static readonly Color Selected = Color.FromArgb(255, 120, 230, 255);

    private static readonly SilkWireBatch Overlay = new();
    private static readonly WorldSolidBatch Panels = new();

    private readonly List<UiButton> _buttons = [];
    private bool _chargeMenuOpen;
    private float _scale = 1f;
    private int _width;
    private int _height;

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
        _buttons.Clear();
        Overlay.Clear();
        Panels.Clear();

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

        DrawTopBar(shot, heroRange, gun, defenseLine, reloadLine);
        DrawGunPanel(gun, orderedElevMils, orderedDeflMils, reloadLine, underMin, predictedRangeMeters, predictedTofSeconds);
        DrawMetPanel(atmosphere, sampleAlt, windSpeed, windBearing, predictedImpact, terrain);
        DrawChargeMenu(gun);
        DrawPaceRow(paceIndex, paceLabel);
        DrawCameraRow(followProjectile);
        DrawFireButton(reloadLine is null && shot.Phase != BallisticTrajectoryPhase.InFlight);
        DrawLegend();
        DrawHelp();

        frame.SetTitle(string.Create(
            CultureInfo.InvariantCulture,
            $"Kristiansand K9  |  {SimulationUnits.FormatRange(heroRange)}  |  {gun.ChargeLabel}  |  {(followProjectile ? "FOLLOW SHELL" : "FOLLOW GUN")}  |  {fps:F0} fps"));

        var ortho = Matrix4x4.CreateOrthographicOffCenter(0f, _width, _height, 0f, -1f, 1f);
        // Solid panels first (dark translucent boxes), then wire text/borders on top.
        if (Panels.Triangles.Length >= 3)
            frame.DrawWorldTriangles(Panels.Triangles, ortho);
        LastOverlayLineCount = Overlay.AsSpan().Length / 2;
        frame.DrawWorldLines(Overlay.AsSpan(), ortho);
    }

    private void DrawTopBar(
        BallisticTrajectoryRunner shot,
        float heroRange,
        GunModel gun,
        string? defenseLine,
        string? reloadLine)
    {
        var topH = 56f * _scale;
        PanelBox(8, 8, _width - 16, topH);

        var phase = shot.Phase switch
        {
            BallisticTrajectoryPhase.InFlight => "IN FLIGHT",
            BallisticTrajectoryPhase.Impacted => "IMPACT",
            _ => reloadLine is null ? "READY" : "BUSY",
        };
        var phaseColor = shot.Phase == BallisticTrajectoryPhase.InFlight ? Accent
            : shot.Phase == BallisticTrajectoryPhase.Impacted ? Alert
            : reloadLine is null ? Ready : Alert;

        HudStickFont.Draw(Overlay, phase, 18 * _scale, 16 * _scale, 2.2f * _scale, phaseColor);
        HudStickFont.Draw(
            Overlay,
            $"{SimulationUnits.FormatRange(heroRange)}   ZONE {gun.ChargeLabel.ToUpperInvariant()}",
            170 * _scale,
            16 * _scale,
            2.0f * _scale,
            Text);

        var mission = string.IsNullOrEmpty(defenseLine) ? "DEFENSE OF KRISTIANSAND — ODDEROYA K9" : defenseLine;
        HudStickFont.Draw(Overlay, Truncate(mission, 78), 18 * _scale, 36 * _scale, 1.5f * _scale, Accent);
    }

    private void DrawGunPanel(
        GunModel gun,
        float orderedElevMils,
        float orderedDeflMils,
        string? reloadLine,
        bool underMin,
        float predictedRange,
        float tof)
    {
        var px = 8f;
        var py = 72f * _scale;
        var pw = 300f * _scale;
        var ph = 168f * _scale;
        PanelBox(px, py, pw, ph);

        var lx = px + 12 * _scale;
        var ly = py + 12 * _scale;
        var ls = 1.65f * _scale;
        var lh = 15f * _scale;

        HudStickFont.Draw(Overlay, "FIRE CONTROL", lx, ly, 1.9f * _scale, Accent);
        ly += lh + 4 * _scale;
        HudStickFont.Draw(Overlay, $"QE    {gun.ElevationMils:F0} -> {orderedElevMils:F0} MIL", lx, ly, ls, Text);
        ly += lh;
        HudStickFont.Draw(Overlay, $"DEFL  {gun.DeflectionMils:F0} -> {orderedDeflMils:F0} MIL", lx, ly, ls, Text);
        ly += lh;
        HudStickFont.Draw(Overlay, $"PRED  {SimulationUnits.FormatRange(predictedRange)}   TOF {tof:F1} S", lx, ly, ls, underMin ? Alert : Text);
        ly += lh;
        if (underMin)
        {
            HudStickFont.Draw(Overlay, $"BELOW HE MIN {SimulationUnits.MinEffectiveRangeMeters / 1000f:F1} KM", lx, ly, ls, Alert);
            ly += lh;
        }

        HudStickFont.Draw(
            Overlay,
            reloadLine is null ? "CREW READY" : Truncate(reloadLine.ToUpperInvariant(), 30),
            lx,
            ly,
            ls,
            reloadLine is null ? Ready : Alert);
    }

    private void DrawMetPanel(
        AtmosphereModel atmosphere,
        float sampleAlt,
        float windSpeed,
        float windBearing,
        Vector3 predictedImpact,
        TerrainWorld terrain)
    {
        var pw = 300f * _scale;
        var ph = 132f * _scale;
        var px = _width - pw - 8;
        var py = 72f * _scale;
        PanelBox(px, py, pw, ph);

        var lx = px + 12 * _scale;
        var ly = py + 12 * _scale;
        var ls = 1.55f * _scale;
        var lh = 15f * _scale;
        var rho = atmosphere.DensityKgPerM3At(sampleAlt);

        HudStickFont.Draw(Overlay, "MET / MPI", lx, ly, 1.9f * _scale, Accent);
        ly += lh + 4 * _scale;
        HudStickFont.Draw(Overlay, $"AIR   {rho:F3} KG/M3  {atmosphere.TemperatureAt(sampleAlt):F0} K", lx, ly, ls, Text);
        ly += lh;
        HudStickFont.Draw(Overlay, $"WIND  {windSpeed:F0} M/S  BRG {windBearing:F0}", lx, ly, ls, Text);
        ly += lh;
        HudStickFont.Draw(Overlay, $"LAT   {KristiansandHeightSampler.MeanLatitudeDegrees:F1} N", lx, ly, ls, Text);
        ly += lh;
        var mpiRange = Horiz(terrain.GunBaseline, predictedImpact);
        HudStickFont.Draw(Overlay, $"MPI   {SimulationUnits.FormatRange(mpiRange)}  H {predictedImpact.Y:F0} M", lx, ly, ls, BallisticArcColors.MpiMark);
    }

    private void DrawChargeMenu(GunModel gun)
    {
        var bw = 210f * _scale;
        var bh = 34f * _scale;
        var bx = 8f;
        var by = _height - 210f * _scale;
        AddButton("charge-toggle", bx, by, bw, bh, $"CHARGE  {gun.ChargeLabel.ToUpperInvariant()}", selected: true);
        DrawButtonVisual(bx, by, bw, bh, $"CHARGE  {gun.ChargeLabel.ToUpperInvariant()}", selected: true);

        if (!_chargeMenuOpen)
            return;

        for (var i = 0; i < SimulationUnits.ChargeLabels.Length; i++)
        {
            var y = by - (i + 1) * (bh + 4 * _scale);
            var label = $"{i + 1} {SimulationUnits.ChargeLabels[i].ToUpperInvariant()}  {SimulationUnits.ChargeSpeedsMps[i]:F0} M/S";
            var id = $"charge-{i}";
            var on = gun.ChargeIndex == i;
            AddButton(id, bx, y, bw, bh, label, on);
            DrawButtonVisual(bx, y, bw, bh, label, on);
        }
    }

    private void DrawPaceRow(int paceIndex, string paceLabel)
    {
        var labels = new[] { "1X", "3X", "8X", "24X" };
        var bw = 64f * _scale;
        var bh = 30f * _scale;
        var gap = 6f * _scale;
        var total = labels.Length * bw + (labels.Length - 1) * gap;
        var x0 = (_width - total) * 0.5f;
        var y = _height - 100f * _scale;
        HudStickFont.Draw(Overlay, "PACE", x0, y - 18 * _scale, 1.4f * _scale, Dim);
        for (var i = 0; i < labels.Length; i++)
        {
            var x = x0 + i * (bw + gap);
            var id = $"pace-{i}";
            AddButton(id, x, y, bw, bh, labels[i], paceIndex == i);
            DrawButtonVisual(x, y, bw, bh, labels[i], paceIndex == i);
        }

        _ = paceLabel;
    }

    private void DrawCameraRow(bool followProjectile)
    {
        var bw = 110f * _scale;
        var bh = 30f * _scale;
        var gap = 6f * _scale;
        var x = _width - 8 - 4 * bw - 3 * gap;
        var y = _height - 100f * _scale;

        void Cam(string id, string label, bool on)
        {
            AddButton(id, x, y, bw, bh, label, on);
            DrawButtonVisual(x, y, bw, bh, label, on);
            x += bw + gap;
        }

        Cam("follow", followProjectile ? "SHELL" : "GUN", true);
        Cam("snap-gun", "GUN VIEW", false);
        Cam("snap-battle", "BATTLE", false);
        Cam("map", "MAP", false);
    }

    private void DrawFireButton(bool canFire)
    {
        var bw = 160f * _scale;
        var bh = 44f * _scale;
        var bx = (_width - bw) * 0.5f;
        var by = _height - 52f * _scale;
        AddButton("fire", bx, by, bw, bh, "FIRE", canFire);
        PanelBox(bx, by, bw, bh, hot: canFire);
        HudStickFont.Rect(Overlay, bx, by, bw, bh, canFire ? Alert : Dim);
        var fireScale = 2.0f * _scale;
        var tw = HudStickFont.MeasureWidth("FIRE", fireScale);
        HudStickFont.Draw(Overlay, "FIRE", bx + Math.Max(6f, (bw - tw) * 0.5f), by + (bh - 7 * fireScale) * 0.5f, fireScale, canFire ? Alert : Dim);
    }

    private void DrawLegend()
    {
        var pw = 220f * _scale;
        var ph = 118f * _scale;
        var px = _width - pw - 8;
        var py = 220f * _scale;
        PanelBox(px, py, pw, ph);

        var lx = px + 12 * _scale;
        var ly = py + 12 * _scale;
        var ls = 1.45f * _scale;
        var lh = 16f * _scale;
        HudStickFont.Draw(Overlay, "ARC LEGEND", lx, ly, 1.7f * _scale, Accent);
        ly += lh + 2 * _scale;
        LegendRow(ref ly, lx, ls, lh, BallisticArcColors.Ordered, "ORDERED LAY");
        LegendRow(ref ly, lx, ls, lh, BallisticArcColors.Tube, "TUBE (LAYING)");
        LegendRow(ref ly, lx, ls, lh, BallisticArcColors.Flight, "LIVE ROUND");
        LegendRow(ref ly, lx, ls, lh, BallisticArcColors.MinRange, "HE MIN RING");
    }

    private void LegendRow(ref float ly, float lx, float ls, float lh, Color color, string label)
    {
        var swatch = 18f * _scale;
        HudStickFont.Rect(Overlay, lx, ly + 2 * _scale, swatch, 8 * _scale, color);
        HudStickFont.FillHatch(Overlay, lx, ly + 2 * _scale, swatch, 8 * _scale, color, 3f * _scale);
        HudStickFont.Draw(Overlay, label, lx + swatch + 8 * _scale, ly, ls, Text);
        ly += lh;
    }

    private void DrawHelp()
    {
        var help = "WASD/ARROWS LAY   SPACE FIRE   C FOLLOW GUN/SHELL   MOUSE CAM/MENUS";
        var hs = 1.35f * _scale;
        var hw = HudStickFont.MeasureWidth(help, hs);
        HudStickFont.Draw(Overlay, help, Math.Max(12f, (_width - hw) * 0.5f), _height - 18f * _scale, hs, Dim);
    }

    private void PanelBox(float x, float y, float w, float h, bool hot = false)
    {
        SolidRect(x, y, w, h, hot ? PanelHotRgba : PanelRgba);
        HudStickFont.Rect(Overlay, x, y, w, h, Border);
        HudStickFont.Rect(Overlay, x + 2, y + 2, w - 4, h - 4, Color.FromArgb(140, Border.R, Border.G, Border.B));
    }

    private static void SolidRect(float x, float y, float w, float h, Rgba32 color)
    {
        // Panel farther than stick text (HudZ=-0.4) so depth-test Less still shows glyphs.
        const float z = 0.2f;
        var a = new Vector3(x, y, z);
        var b = new Vector3(x + w, y, z);
        var c = new Vector3(x + w, y + h, z);
        var d = new Vector3(x, y + h, z);
        // Both windings — ortho Y-down + back-face cull must not eat the panel.
        Panels.Triangle(a, b, c, color);
        Panels.Triangle(a, c, d, color);
        Panels.Triangle(a, c, b, color);
        Panels.Triangle(a, d, c, color);
    }

    private void AddButton(string id, float x, float y, float w, float h, string label, bool selected)
        => _buttons.Add(new UiButton(id, x, y, w, h, label, selected));

    private void DrawButtonVisual(float x, float y, float w, float h, string label, bool selected, Color? force = null)
    {
        var border = force ?? (selected ? Selected : Border);
        SolidRect(x, y, w, h, selected ? PanelHotRgba : PanelRgba);
        HudStickFont.Rect(Overlay, x, y, w, h, border);
        var ls = 1.55f * _scale;
        var tw = HudStickFont.MeasureWidth(label, ls);
        HudStickFont.Draw(Overlay, label, x + Math.Max(6f, (w - tw) * 0.5f), y + (h - 7 * ls) * 0.5f, ls, force ?? (selected ? Selected : Text));
    }

    private bool HitAny(float mx, float my)
    {
        foreach (var btn in _buttons)
        {
            if (btn.Contains(mx, my))
                return true;
        }

        // Approximate panel hit areas so dragging over chrome does not orbit.
        if (mx < 320 * _scale && my < 260 * _scale)
            return true;
        if (mx > _width - 320 * _scale && my < 360 * _scale)
            return true;
        if (my > _height - 220 * _scale)
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

    private readonly record struct UiButton(string Id, float X, float Y, float W, float H, string Label, bool Selected)
    {
        public bool Contains(float mx, float my) =>
            mx >= X && my >= Y && mx <= X + W && my <= Y + H;
    }
}
