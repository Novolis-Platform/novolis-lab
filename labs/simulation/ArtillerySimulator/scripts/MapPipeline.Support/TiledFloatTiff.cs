namespace ArtillerySimulator.MapPipeline;

/// <summary>Minimal little-endian tiled float32 TIFF decoder for Kartverket NHM exportImage.</summary>
public static class TiledFloatTiff
{
    public static float[] Decode(byte[] bytes, out int width, out int height)
    {
        static uint U32(byte[] b, int o) => System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(o));
        static ushort U16(byte[] b, int o) => System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(o));

        if (bytes.Length < 8 || bytes[0] != (byte)'I' || bytes[1] != (byte)'I')
            throw new InvalidDataException($"Not a little-endian TIFF ({bytes.Length} bytes).");

        var ifd = (int)U32(bytes, 4);
        var n = U16(bytes, ifd);
        uint w = 0, h = 0, bps = 0, comp = 0, sf = 0, tileW = 0, tileH = 0;
        var tileOffPtr = 0;
        var tileBytePtr = 0;
        var tileCount = 0;
        for (var i = 0; i < n; i++)
        {
            var o = ifd + 2 + i * 12;
            var tag = U16(bytes, o);
            var type = U16(bytes, o + 2);
            var count = U32(bytes, o + 4);
            var val = U32(bytes, o + 8);
            var s = (ushort)(val & 0xFFFF);
            switch (tag)
            {
                case 256: w = type == 3 ? s : val; break;
                case 257: h = type == 3 ? s : val; break;
                case 258: bps = type == 3 ? s : val; break;
                case 259: comp = type == 3 ? s : val; break;
                case 322: tileW = type == 3 ? s : val; break;
                case 323: tileH = type == 3 ? s : val; break;
                case 324: tileOffPtr = (int)val; tileCount = (int)count; break;
                case 325: tileBytePtr = (int)val; break;
                case 339: sf = type == 3 ? s : val; break;
            }
        }

        if (comp != 1 || bps != 32 || sf != 3 || tileW == 0 || tileH == 0 || tileCount == 0)
            throw new InvalidDataException($"Unsupported TIFF (comp={comp} bps={bps} sf={sf} tile={tileW}x{tileH}).");

        width = (int)w;
        height = (int)h;
        var tilesX = (int)((w + tileW - 1) / tileW);
        var tilesY = (int)((h + tileH - 1) / tileH);
        var grid = new float[width * height];
        Array.Fill(grid, float.NaN);
        for (var ty = 0; ty < tilesY; ty++)
        {
            for (var tx = 0; tx < tilesX; tx++)
            {
                var ti = ty * tilesX + tx;
                if (ti >= tileCount)
                    continue;
                var off = (int)U32(bytes, tileOffPtr + ti * 4);
                var len = (int)U32(bytes, tileBytePtr + ti * 4);
                if (off == 0 || len == 0)
                    continue;
                for (var row = 0; row < tileH; row++)
                {
                    var gy = ty * (int)tileH + row;
                    if (gy >= height)
                        break;
                    for (var col = 0; col < tileW; col++)
                    {
                        var gx = tx * (int)tileW + col;
                        if (gx >= width)
                            break;
                        grid[gy * width + gx] = BitConverter.ToSingle(bytes, off + (row * (int)tileW + col) * 4);
                    }
                }
            }
        }

        return grid;
    }

    public static float SampleBilinear(float[] src, int w, int h, double x, double y)
    {
        var x0 = (int)Math.Floor(x);
        var y0 = (int)Math.Floor(y);
        var x1 = Math.Min(x0 + 1, w - 1);
        var y1 = Math.Min(y0 + 1, h - 1);
        x0 = Math.Clamp(x0, 0, w - 1);
        y0 = Math.Clamp(y0, 0, h - 1);
        var tx = x - x0;
        var ty = y - y0;
        var a = src[y0 * w + x0];
        var b = src[y0 * w + x1];
        var c = src[y1 * w + x0];
        var d = src[y1 * w + x1];
        static float N(float v) => float.IsNaN(v) ? 0f : v;
        var top = N(a) + (N(b) - N(a)) * (float)tx;
        var bot = N(c) + (N(d) - N(c)) * (float)tx;
        return top + (bot - top) * (float)ty;
    }
}
