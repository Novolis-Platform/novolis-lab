using Novolis.Silk;

namespace ArtillerySimulator;

internal sealed class PlayingFrameRenderer
{
    private readonly BattleWorld _world;
    private readonly ArtillerySceneRenderer _renderer;
    private readonly ArtilleryRunOptions _options;
    private readonly ArtilleryFrameProfileController _profiles;

    public PlayingFrameRenderer(
        BattleWorld world,
        ArtillerySceneRenderer renderer,
        ArtilleryRunOptions options,
        ArtilleryFrameProfileController profiles)
    {
        _world = world;
        _renderer = renderer;
        _options = options;
        _profiles = profiles;
    }

    public void Render(SilkFrame frame)
    {
        if (_options.Smoke)
            return;

        var vm = _world.CreateFrameViewModel();
        _renderer.DrawPlaying(frame, vm, _profiles.ActiveProfiler());
        _profiles.ProfileOverlayIfNeeded(frame, vm);
    }
}
