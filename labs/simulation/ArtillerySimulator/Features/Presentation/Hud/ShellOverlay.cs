using System.Globalization;
using Novolis.Silk;
using SkiaSharp;

namespace ArtillerySimulator;

/// <summary>Splash, loading, pause, and license overlays (Skia blit on top of the frame).</summary>
internal sealed class ShellOverlay : IDisposable
{
    private readonly ShellOverlaySurface _surface = new();

    private SKBitmap? _splash;
    private SKBitmap? _icon;
    private string _lastSignature = "";
    private float _legalScrollLines;
    private float _legalMaxScrollLines;

    public float LegalScrollLines
    {
        get => _legalScrollLines;
        set
        {
            var next = Math.Clamp(value, 0f, Math.Max(0f, _legalMaxScrollLines));
            if (MathF.Abs(next - _legalScrollLines) < 0.01f)
                return;
            _legalScrollLines = next;
            _lastSignature = "";
        }
    }

    public void ResetLegalScroll()
    {
        _legalScrollLines = 0f;
        _lastSignature = "";
    }

    public void NudgeLegalScroll(float deltaLines) =>
        LegalScrollLines = _legalScrollLines + deltaLines;

    public void EnsureAssetsLoaded()
    {
        _splash ??= ShellOverlayBitmaps.TryLoad("assets/k9-dog-splash.png")
            ?? ShellOverlayBitmaps.TryLoad("assets/k9-dog-splash.jpg");
        _icon ??= ShellOverlayBitmaps.TryLoad("assets/k9-dog-icon.png")
            ?? ShellOverlayBitmaps.TryLoad("assets/k9-dog-icon.jpg");
    }

    public void Draw(SilkFrame frame, ShellPhase phase, string statusLine, IGameLevel? level = null)
    {
        EnsureAssetsLoaded();
        var resized = _surface.Ensure(frame.Width, frame.Height);
        if (resized)
            _lastSignature = "";

        var levelKey = level is null ? "" : $"{level.Id}|{level.DisplayName}";
        var signature = string.Create(
            CultureInfo.InvariantCulture,
            $"{phase}|{statusLine}|{levelKey}|{_surface.Width}x{_surface.Height}|{GameLevelCatalog.PlayableCount}|{_legalScrollLines:F1}");
        if (signature != _lastSignature)
        {
            _lastSignature = signature;
            var canvas = _surface.Inner.Canvas;
            canvas.Clear(ShellOverlayTheme.CanvasClear);
            switch (phase)
            {
                case ShellPhase.Splash:
                    ShellSplashPanel.Draw(canvas, _surface.Width, _surface.Height, _splash, _icon, level, statusLine);
                    break;
                case ShellPhase.Loading:
                    ShellLoadingPanel.Draw(canvas, _surface.Width, _surface.Height, _splash, _icon, level, statusLine);
                    break;
                case ShellPhase.Paused:
                    ShellPausePanel.Draw(canvas, _surface.Width, _surface.Height, ref _legalScrollLines, out _legalMaxScrollLines);
                    break;
                case ShellPhase.Legal:
                    ShellLegalPanel.Draw(canvas, _surface.Width, _surface.Height, level, ref _legalScrollLines, out _legalMaxScrollLines);
                    break;
            }

            _surface.Inner.CopyPixels();
        }

        _surface.Blit(frame);
        frame.SetTitle(ShellOverlayTitle.For(phase, level));
    }

    public void Dispose()
    {
        _surface.Dispose();
        _splash?.Dispose();
        _icon?.Dispose();
    }
}
