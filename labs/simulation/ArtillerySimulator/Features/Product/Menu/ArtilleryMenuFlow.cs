using Novolis.Game.MenuFlows;

namespace ArtillerySimulator;

/// <summary>
/// Host-side navigation over <see cref="GameScreenStack"/> for splash / loading / pause / licenses.
/// Empty stack means the battle is live (playing).
/// </summary>
internal sealed class ArtilleryMenuFlow
{
    private readonly GameScreenStack _stack = new();

    public string? CurrentId => _stack.Current?.ScreenId;

    public bool IsSplash => CurrentId == ArtilleryScreenIds.Splash;
    public bool IsLoading => CurrentId == ArtilleryScreenIds.Loading;
    public bool IsPaused => CurrentId == ArtilleryScreenIds.Pause;
    public bool IsLegal => CurrentId == ArtilleryScreenIds.Legal;
    public bool IsPlaying => CurrentId is null;
    public bool BlocksBattle => !IsPlaying;

    public ShellPhase Phase => CurrentId switch
    {
        ArtilleryScreenIds.Splash => ShellPhase.Splash,
        ArtilleryScreenIds.Loading => ShellPhase.Loading,
        ArtilleryScreenIds.Pause => ShellPhase.Paused,
        ArtilleryScreenIds.Legal => ShellPhase.Legal,
        _ => ShellPhase.Playing,
    };

    public void ShowSplash()
    {
        Clear();
        Push(new ArtillerySplashScreen());
    }

    public void ShowLoading()
    {
        Clear();
        Push(new ArtilleryLoadingScreen());
    }

    public void ClearToPlaying() => Clear();

    public void PushPause()
    {
        if (IsPaused || IsLegal)
            return;
        Push(new ArtilleryPauseScreen());
    }

    public void PushLegal()
    {
        if (IsLegal)
            return;
        Push(new ArtilleryLegalScreen());
    }

    public void PopOverlay()
    {
        if (CurrentId is ArtilleryScreenIds.Pause or ArtilleryScreenIds.Legal)
            Pop();
    }

    public void TogglePause()
    {
        if (IsLegal)
        {
            Pop();
            return;
        }

        if (IsPaused)
            Pop();
        else if (IsPlaying)
            PushPause();
    }

    public void ToggleLegal()
    {
        if (IsLegal)
            Pop();
        else
            PushLegal();
    }

    private void Clear()
    {
        while (_stack.Current is not null)
            Pop();
    }

    private void Push(IGameScreen screen) =>
        _stack.PushAsync(screen).GetAwaiter().GetResult();

    private void Pop() =>
        _stack.PopAsync().GetAwaiter().GetResult();
}
