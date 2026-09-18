using System.Numerics;

namespace RtsLite.Game;

internal sealed class RtsSelection
{
    private Vector2? _dragStart;
    private bool _dragging;
    private bool _wasLeftDown;

    public void Update(
        IList<RtsUnit> units,
        Func<Vector2, Vector3> screenToGround,
        bool leftPressed,
        bool leftDown,
        bool rightPressed,
        Vector2 mouse)
    {
        var leftReleased = _wasLeftDown && !leftDown;
        _wasLeftDown = leftDown;

        if (leftPressed)
        {
            _dragStart = mouse;
            _dragging = true;
        }

        if (leftReleased && _dragging)
        {
            _dragging = false;
            var start = _dragStart ?? mouse;
            var rect = NormalizedRect(start, mouse);
            if (rect.Width < 6f && rect.Height < 6f)
                SelectSingle(units, screenToGround(mouse));
            else
                SelectBox(units, screenToGround, rect);
            _dragStart = null;
        }

        if (rightPressed)
        {
            var target = screenToGround(mouse);
            foreach (var unit in units)
            {
                if (unit.Team == UnitTeam.Player && unit.Selected)
                    unit.MoveTarget = target;
            }
        }
    }

    public bool IsDragging => _dragging;

    public ScreenRect? ActiveDragRect(Vector2 mouse) =>
        _dragging && _dragStart is { } start ? NormalizedRect(start, mouse) : null;

    private static void SelectSingle(IList<RtsUnit> units, Vector3 ground)
    {
        var best = -1;
        var bestDist = float.MaxValue;
        for (var i = 0; i < units.Count; i++)
        {
            if (units[i].Team != UnitTeam.Player)
                continue;

            var d = Vector3.DistanceSquared(units[i].Position, ground);
            if (d < bestDist)
            {
                bestDist = d;
                best = i;
            }
        }

        foreach (var unit in units)
            unit.Selected = false;

        if (best >= 0 && bestDist < RtsUnit.Radius * RtsUnit.Radius * 9f)
            units[best].Selected = true;
    }

    private static void SelectBox(
        IList<RtsUnit> units,
        Func<Vector2, Vector3> screenToGround,
        ScreenRect rect)
    {
        var a = screenToGround(new Vector2(rect.Left, rect.Top));
        var b = screenToGround(new Vector2(rect.Right, rect.Bottom));
        var minX = MathF.Min(a.X, b.X);
        var maxX = MathF.Max(a.X, b.X);
        var minZ = MathF.Min(a.Z, b.Z);
        var maxZ = MathF.Max(a.Z, b.Z);

        foreach (var unit in units)
        {
            if (unit.Team != UnitTeam.Player)
            {
                unit.Selected = false;
                continue;
            }

            var p = unit.Position;
            unit.Selected = p.X >= minX && p.X <= maxX && p.Z >= minZ && p.Z <= maxZ;
        }
    }

    private static ScreenRect NormalizedRect(Vector2 a, Vector2 b)
    {
        var x = MathF.Min(a.X, b.X);
        var y = MathF.Min(a.Y, b.Y);
        return new ScreenRect(x, y, MathF.Abs(b.X - a.X), MathF.Abs(b.Y - a.Y));
    }
}

internal readonly struct ScreenRect(float x, float y, float width, float height)
{
    public float X { get; } = x;
    public float Y { get; } = y;
    public float Width { get; } = width;
    public float Height { get; } = height;
    public float Left => X;
    public float Top => Y;
    public float Right => X + Width;
    public float Bottom => Y + Height;
}
