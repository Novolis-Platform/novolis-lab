namespace CursorRemote.Services;

/// <summary>
/// Pinch zoom relative to the gesture start. Callers must keep
/// <paramref name="baseZoom"/> and <paramref name="startDistance"/> fixed
/// for the whole pinch — rebasing each frame compounds and explodes.
/// </summary>
public static class RemotePinchZoom
{
    public static double FromDistances(
        double baseZoom,
        double startDistance,
        double currentDistance,
        double minimumZoom,
        double maximumZoom)
    {
        if (startDistance <= 0
            || currentDistance <= 0
            || double.IsNaN(baseZoom)
            || double.IsNaN(startDistance)
            || double.IsNaN(currentDistance))
        {
            return Math.Clamp(baseZoom, minimumZoom, maximumZoom);
        }

        var ratio = currentDistance / startDistance;
        // Soften finger travel so a full stretch is intentional, not jumpy.
        var softened = Math.Pow(ratio, 0.85);
        return Math.Clamp(baseZoom * softened, minimumZoom, maximumZoom);
    }
}
