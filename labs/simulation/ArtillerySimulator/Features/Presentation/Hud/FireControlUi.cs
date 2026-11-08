using System.Globalization;
using System.Numerics;
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
    private readonly List<FireControlUiButton> _buttons = [];
    private readonly SKTypeface _typeface;
    private readonly SkiaOverlaySurface _surface = new();
    private bool _chargeMenuOpen;
    private string _lastSignature = "";
    private float _rasterAge = 99f;
    private float _titleAge = 99f;
    private int _lastSurfaceWidth;
    private int _lastSurfaceHeight;

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
        var metrics = FireControlHudMetrics.FromFrame(frame.Width, frame.Height);
        var mx = frame.MouseX;
        var my = frame.MouseY;
        PointerOverUi = FireControlHitTester.HitAny(_buttons, mx, my, metrics);

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
        bool launchDetonation,
        string? radioLine = null,
        string? radioSpeaker = null,
        bool voiceAvailable = false,
        bool scoreAvailable = false,
        bool audioHubPresent = false,
        ScoreSnapshot score = default,
        string? batteryLabel = null)
    {
        var metrics = FireControlHudMetrics.FromFrame(frame.Width, frame.Height);
        var width = metrics.Width;
        var height = metrics.Height;

        if (_lastSurfaceWidth != width || _lastSurfaceHeight != height)
        {
            _lastSignature = "";
            _lastSurfaceWidth = width;
            _lastSurfaceHeight = height;
        }

        _surface.Ensure(width, height);

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
            BallisticTrajectoryPhase.InFlight => FireControlHudMath.HorizontalDistance(terrain.GunBaseline, shot.CurrentPosition),
            BallisticTrajectoryPhase.Impacted when shot.Impact is { } hit => hit.HorizontalRangeMeters,
            _ => predictedRangeMeters,
        };
        var canFire = fireBlockReason is null;

        FireControlLayout.Build(_buttons, metrics, gun, followProjectile, canFire, _chargeMenuOpen);

        var signature = string.Create(
            CultureInfo.InvariantCulture,
            $"{width}x{height}|{gun.ElevationMils:F0}|{orderedElevMils:F0}|{gun.DeflectionMils:F0}|{orderedDeflMils:F0}|{(int)(predictedRangeMeters / 25f)}|{(int)(predictedTofSeconds * 2f)}|{(int)(heroRange / 25f)}|{gun.ChargeIndex}|{followProjectile}|{_chargeMenuOpen}|{canFire}|{defenseLine}|{fireBlockReason}|{shot.Phase}|{solutionUnsafe}|{launchDetonation}|{(int)(windSpeed * 2f)}|{(int)windFrom}|{(int)tempC}|{(int)qnh}|{(int)rh}|{(int)(predictedImpact.Y / 10f)}|{radioSpeaker}|{radioLine}|{voiceAvailable}|{scoreAvailable}|{audioHubPresent}|{score.Points}|{score.HighScore}|{score.TimeText}|{score.IsNewHighScore}|{batteryLabel}|{gun.Spec.Id}");
        _rasterAge += frame.DeltaSeconds;
        var dirty = signature != _lastSignature || _rasterAge >= 0.25f;
        if (dirty)
        {
            _lastSignature = signature;
            _rasterAge = 0f;
            var chrome = new FireControlChromePrimitives(_typeface, metrics.Scale);
            var canvas = _surface.Canvas;
            canvas.Clear(SKColors.Transparent);
            FireControlTopBarPanel.Draw(canvas, chrome, metrics, shot, heroRange, gun, defenseLine, fireBlockReason, launchDetonation, solutionUnsafe, score);
            FireControlGunPanel.Draw(canvas, chrome, metrics, gun, orderedElevMils, orderedDeflMils, fireBlockReason, solutionUnsafe, launchDetonation, predictedRangeMeters, predictedTofSeconds, batteryLabel);
            FireControlMetPanel.Draw(canvas, chrome, metrics, atmosphere, sampleAlt, windSpeed, windFrom, tempC, qnh, rh, predictedImpact, terrain);
            FireControlChargeMenuPanel.Draw(canvas, chrome, metrics, gun, _chargeMenuOpen);
            FireControlCameraRowPanel.Draw(canvas, chrome, metrics, followProjectile);
            FireControlFireButtonPanel.Draw(canvas, chrome, metrics, canFire);
            FireControlLegendPanel.Draw(canvas, chrome, metrics, solutionUnsafe);
            FireControlHelpPanel.Draw(canvas, chrome, metrics);
            FireControlRadioPanel.Draw(canvas, chrome, metrics, radioSpeaker, radioLine, voiceAvailable, scoreAvailable, audioHubPresent);
            _surface.CopyPixels();
            _surface.Upload(frame, width, height);
        }
        else if (_surface.OverlayUploaded)
        {
            frame.DrawOverlay();
        }

        _titleAge += frame.DeltaSeconds;
        if (_titleAge >= 0.25f)
        {
            _titleAge = 0f;
            frame.SetTitle(string.Create(
                CultureInfo.InvariantCulture,
                $"{ProductIdentity.Title}  |  {score.Points} pts  {score.TimeText}  |  {SimulationUnits.FormatRange(heroRange)}  |  {gun.ChargeLabel}  |  {(followProjectile ? "FOLLOW SHELL" : "FOLLOW GUN")}  |  {fps:F0} fps"));
        }

        LastOverlayLineCount = 0;
    }

    public void Dispose()
    {
        _surface.Dispose();
        _typeface.Dispose();
    }
}
