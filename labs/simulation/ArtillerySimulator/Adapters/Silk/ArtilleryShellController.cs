using Microsoft.Extensions.Logging;
using Novolis.Math.Geometry;
using Novolis.Silk;

namespace ArtillerySimulator;

/// <summary>Splash / loading / pause / legal overlay input + draw.</summary>
internal sealed class ArtilleryShellController
{
    private readonly ArtilleryMenuFlow _menus;
    private readonly ShellOverlay _shell;
    private readonly ILogger<ArtilleryShellController> _log;
    private IGameLevel _level;
    private string _loadingStatus = "Loading map…";
    private bool _loadFailed;

    public ArtilleryShellController(
        ArtilleryMenuFlow menus,
        ShellOverlay shell,
        ArtilleryRunOptions options,
        ILogger<ArtilleryShellController> log)
    {
        _menus = menus;
        _shell = shell;
        _level = options.Level;
        _log = log;
        if (!options.SkipShell)
            _menus.ShowSplash();
    }

    public ArtilleryMenuFlow Menus => _menus;
    public ShellOverlay Shell => _shell;
    public IGameLevel Level => _level;
    public string LoadingStatus => _loadingStatus;
    public bool IsPaused => _menus.IsPaused || _menus.IsLegal;

    public void SetLevel(IGameLevel level) => _level = level;

    public void SetLoadingStatus(string status) => _loadingStatus = status;

    public void EnsureAssets() => _shell.EnsureAssetsLoaded();

    /// <returns>True when the frame was fully handled as a shell overlay (caller should return).</returns>
    public bool TryHandlePreWorldFrame(SilkFrame frame, bool worldReady, Action loadWorld)
    {
        if (_menus.IsSplash || (_menus.IsLegal && !worldReady))
        {
            frame.Clear(Rgba32.Black);
            var status = _menus.IsSplash
                ? ProductLegal.SplashControls
                : ProductLegal.LegalCloseHint;
            _shell.Draw(frame, _menus.Phase, status, _level);
            return true;
        }

        if (_menus.IsLoading)
        {
            frame.Clear(Rgba32.Black);
            var status = _loadFailed
                ? $"{_loadingStatus}  Esc — back to title"
                : _loadingStatus;
            _shell.Draw(frame, _menus.Phase, status, _level);
            if (!worldReady && !_loadFailed)
            {
                _loadingStatus = $"Building {_level.DisplayName}…";
                _log.LogInformation("Loading world for {LevelId}", _level.Id);
                try
                {
                    loadWorld();
                    _loadingStatus = "Ready";
                    _menus.ClearToPlaying();
                    _log.LogInformation("World ready — entering play");
                }
                catch (Exception ex)
                {
                    _loadFailed = true;
                    _loadingStatus = $"Load failed: {ex.Message}";
                    _log.LogError(ex, "World load failed for {LevelId}", _level.Id);
                    Console.Error.WriteLine($"World load failed: {ex.Message}");
                }
            }

            return true;
        }

        return false;
    }

    public void DrawOverlayIfNeeded(SilkFrame frame)
    {
        if (_menus.IsPaused || _menus.IsLegal)
            _shell.Draw(frame, _menus.Phase, "", _level);
    }

    public void HandleKeys(SilkFrame frame, bool worldReady, Action<int> selectLevel)
    {
        if (_menus.IsSplash)
        {
            if (GameLevelCatalog.PlayableCount > 1)
            {
                if (frame.IsKeyPressed(Key.Left) || frame.IsKeyPressed(Key.A))
                {
                    selectLevel(-1);
                    return;
                }

                if (frame.IsKeyPressed(Key.Right) || frame.IsKeyPressed(Key.D))
                {
                    selectLevel(+1);
                    return;
                }
            }

            if (frame.IsKeyPressed(Key.L))
            {
                _shell.ResetLegalScroll();
                _menus.PushLegal();
                _log.LogDebug("Opened licenses from splash");
                return;
            }

            if (frame.IsKeyPressed(Key.Enter) || frame.IsKeyPressed(Key.Space))
            {
                _loadFailed = false;
                _loadingStatus = "Loading map…";
                _menus.ShowLoading();
            }

            return;
        }

        if (_menus.IsLoading && _loadFailed && frame.IsKeyPressed(Key.Escape))
        {
            _loadFailed = false;
            _loadingStatus = "Loading map…";
            _menus.ShowSplash();
            return;
        }

        if (_menus.IsLegal)
        {
            HandleLegalScroll(frame);
            if (frame.IsKeyPressed(Key.Escape)
                || frame.IsKeyPressed(Key.L)
                || frame.IsKeyPressed(Key.Enter))
                _menus.PopOverlay();
            return;
        }

        if (!worldReady)
            return;

        if (frame.IsKeyPressed(Key.L))
        {
            if (!_menus.IsLegal)
                _shell.ResetLegalScroll();
            _menus.ToggleLegal();
            return;
        }

        if (frame.IsKeyPressed(Key.Escape) || frame.IsKeyPressed(Key.P))
        {
            _menus.TogglePause();
            return;
        }

        if (_menus.IsPaused && frame.IsKeyPressed(Key.Enter))
            _menus.PopOverlay();
    }

    private void HandleLegalScroll(SilkFrame frame)
    {
        if (MathF.Abs(frame.ScrollDelta) > 0.001f)
            _shell.NudgeLegalScroll(-frame.ScrollDelta * 3f);
        if (frame.IsKeyPressed(Key.Up) || frame.IsKeyPressed(Key.W))
            _shell.NudgeLegalScroll(-8f);
        if (frame.IsKeyPressed(Key.Down) || frame.IsKeyPressed(Key.S))
            _shell.NudgeLegalScroll(+8f);
        if (frame.IsKeyDown(Key.Up) || frame.IsKeyDown(Key.W))
            _shell.NudgeLegalScroll(-frame.DeltaSeconds * 14f);
        if (frame.IsKeyDown(Key.Down) || frame.IsKeyDown(Key.S))
            _shell.NudgeLegalScroll(+frame.DeltaSeconds * 14f);
    }
}
