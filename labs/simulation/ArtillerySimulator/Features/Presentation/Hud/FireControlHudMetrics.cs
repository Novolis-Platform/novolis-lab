namespace ArtillerySimulator;

internal readonly struct FireControlHudMetrics(float scale, int width, int height)
{
    public float Scale { get; } = scale;
    public int Width { get; } = width;
    public int Height { get; } = height;

    public static FireControlHudMetrics FromFrame(int width, int height)
    {
        var scale = Math.Clamp(Math.Min(width, height) / 900f, 0.85f, 1.4f);
        return new FireControlHudMetrics(scale, width, height);
    }
}
