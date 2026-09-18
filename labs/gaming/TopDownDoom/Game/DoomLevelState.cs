using Novolis.Rendering.TwoD;

namespace TopDownDoom.Game;

internal sealed class DoomLevelState
{
    private readonly List<TwoDCollider> _sessionColliders = [];

    public readonly List<TwoDStaticPolygon> BlueGateVisuals = [];
    public readonly List<TwoDCollider> BlueGateColliders = [];
    public readonly List<TwoDStaticPolygon> ClosetNorthVisuals = [];
    public readonly List<TwoDCollider> ClosetNorthColliders = [];
    public readonly List<TwoDStaticPolygon> ClosetEastVisuals = [];
    public readonly List<TwoDCollider> ClosetEastColliders = [];

    public bool CorridorLessonTriggered { get; set; }
    public bool BlueGateOpen { get; set; }
    public bool ClosetsOpened { get; set; }

    public void CaptureCollision(TwoDScene scene)
    {
        _sessionColliders.Clear();
        foreach (var c in scene.Collision.StaticColliders)
        {
            _sessionColliders.Add(c);
        }
    }

    public void ResetProgress()
    {
        CorridorLessonTriggered = false;
        BlueGateOpen = false;
        ClosetsOpened = false;
        BlueGateVisuals.Clear();
        BlueGateColliders.Clear();
        ClosetNorthVisuals.Clear();
        ClosetNorthColliders.Clear();
        ClosetEastVisuals.Clear();
        ClosetEastColliders.Clear();
        _sessionColliders.Clear();
    }

    public void OpenBlueGate(TwoDScene scene)
    {
        if (BlueGateOpen)
        {
            return;
        }

        BlueGateOpen = true;
        RemoveBlocks(scene, BlueGateVisuals, BlueGateColliders);
    }

    public void OpenClosets(TwoDScene scene)
    {
        if (ClosetsOpened)
        {
            return;
        }

        ClosetsOpened = true;
        RemoveBlocks(scene, ClosetNorthVisuals, ClosetNorthColliders);
        RemoveBlocks(scene, ClosetEastVisuals, ClosetEastColliders);
    }

    private void RemoveBlocks(
        TwoDScene scene,
        List<TwoDStaticPolygon> visuals,
        List<TwoDCollider> colliders)
    {
        foreach (var v in visuals)
        {
            scene.StaticPolygons.Remove(v);
        }

        visuals.Clear();
        foreach (var c in colliders)
        {
            _sessionColliders.Remove(c);
        }

        colliders.Clear();
        RebuildCollision(scene);
    }

    private void RebuildCollision(TwoDScene scene)
    {
        scene.Collision.Clear();
        foreach (var c in _sessionColliders)
        {
            scene.Collision.AddStatic(c);
        }
    }
}
