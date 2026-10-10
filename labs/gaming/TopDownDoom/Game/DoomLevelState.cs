using Novolis.Rendering.Planar;

namespace TopDownDoom.Game;

internal sealed class DoomLevelState
{
    private readonly List<PlanarCollider> _sessionColliders = [];

    public readonly List<PlanarStaticPolygon> BlueGateVisuals = [];
    public readonly List<PlanarCollider> BlueGateColliders = [];
    public readonly List<PlanarStaticPolygon> ClosetNorthVisuals = [];
    public readonly List<PlanarCollider> ClosetNorthColliders = [];
    public readonly List<PlanarStaticPolygon> ClosetEastVisuals = [];
    public readonly List<PlanarCollider> ClosetEastColliders = [];

    public bool CorridorLessonTriggered { get; set; }
    public bool BlueGateOpen { get; set; }
    public bool ClosetsOpened { get; set; }

    public void CaptureCollision(PlanarScene scene)
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

    public void OpenBlueGate(PlanarScene scene)
    {
        if (BlueGateOpen)
        {
            return;
        }

        BlueGateOpen = true;
        RemoveBlocks(scene, BlueGateVisuals, BlueGateColliders);
    }

    public void OpenClosets(PlanarScene scene)
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
        PlanarScene scene,
        List<PlanarStaticPolygon> visuals,
        List<PlanarCollider> colliders)
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

    private void RebuildCollision(PlanarScene scene)
    {
        scene.Collision.Clear();
        foreach (var c in _sessionColliders)
        {
            scene.Collision.AddStatic(c);
        }
    }
}
