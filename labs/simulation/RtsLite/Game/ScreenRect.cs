using System.Numerics;

namespace RtsLite.Game;

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
