namespace ArtillerySimulator.MapPipeline;

/// <summary>
/// Open-sea component of a binary water mask: 4-connected flood fill from image-border water.
/// Inland lakes (not connected to the border) keep separate elevation.
/// </summary>
public static class WaterSeaConnected
{
    /// <summary>Returns a byte grid (1 = sea-connected water) matching <paramref name="water"/>.</summary>
    public static byte[] Mark(byte[] water, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(water);
        if (water.Length != width * height)
            throw new ArgumentException("water length must equal width*height.", nameof(water));

        var sea = new byte[water.Length];
        var queue = new Queue<int>();

        void TryEnqueue(int i)
        {
            if (water[i] == 0 || sea[i] != 0)
                return;
            sea[i] = 1;
            queue.Enqueue(i);
        }

        for (var x = 0; x < width; x++)
        {
            TryEnqueue(x);
            TryEnqueue((height - 1) * width + x);
        }

        for (var y = 0; y < height; y++)
        {
            TryEnqueue(y * width);
            TryEnqueue(y * width + (width - 1));
        }

        while (queue.Count > 0)
        {
            var i = queue.Dequeue();
            var x = i % width;
            var y = i / width;
            if (x > 0) TryEnqueue(i - 1);
            if (x + 1 < width) TryEnqueue(i + 1);
            if (y > 0) TryEnqueue(i - width);
            if (y + 1 < height) TryEnqueue(i + width);
        }

        return sea;
    }

    /// <summary>Forces sea-connected water cells in <paramref name="heights"/> to 0 m.</summary>
    public static int FlattenSeaHeights(short[] heights, byte[] water, byte[] sea)
    {
        ArgumentNullException.ThrowIfNull(heights);
        ArgumentNullException.ThrowIfNull(water);
        ArgumentNullException.ThrowIfNull(sea);
        if (heights.Length != water.Length || water.Length != sea.Length)
            throw new ArgumentException("heights/water/sea length mismatch.");

        var count = 0;
        for (var i = 0; i < heights.Length; i++)
        {
            if (water[i] == 0 || sea[i] == 0)
                continue;
            heights[i] = 0;
            count++;
        }

        return count;
    }
}
