using System.Buffers.Binary;
using System.IO.Compression;
using Novolis.Math.Geometry;

namespace CalypsoCad.Services;

/// <summary>Minimal top-left RGBA → PNG encoder (no third-party image libs).</summary>
internal static class RgbaPng
{
    public static byte[] Encode(ReadOnlySpan<Rgba32> pixels, int width, int height)
    {
        if (width <= 0 || height <= 0 || pixels.Length < width * height)
            throw new ArgumentException("Invalid pixel buffer.");

        using var ms = new MemoryStream();
        // signature
        ms.Write([137, 80, 78, 71, 13, 10, 26, 10]);

        Span<byte> ihdr = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr[..4], width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.Slice(4, 4), height);
        ihdr[8] = 8;  // bit depth
        ihdr[9] = 6;  // RGBA
        ihdr[10] = 0;
        ihdr[11] = 0;
        ihdr[12] = 0;
        WriteChunk(ms, "IHDR"u8, ihdr);

        var raw = new byte[(width * 4 + 1) * height];
        for (var y = 0; y < height; y++)
        {
            var row = y * (width * 4 + 1);
            raw[row] = 0; // filter None
            for (var x = 0; x < width; x++)
            {
                var p = pixels[y * width + x];
                var o = row + 1 + x * 4;
                raw[o] = p.R;
                raw[o + 1] = p.G;
                raw[o + 2] = p.B;
                raw[o + 3] = p.A;
            }
        }

        using (var idat = new MemoryStream())
        {
            // zlib wrapper around deflate
            idat.WriteByte(0x78);
            idat.WriteByte(0x01);
            using (var deflate = new DeflateStream(idat, CompressionLevel.Fastest, leaveOpen: true))
                deflate.Write(raw, 0, raw.Length);
            var adler = Adler32(raw);
            Span<byte> adlerBytes = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(adlerBytes, adler);
            idat.Write(adlerBytes);
            WriteChunk(ms, "IDAT"u8, idat.ToArray());
        }

        WriteChunk(ms, "IEND"u8, ReadOnlySpan<byte>.Empty);
        return ms.ToArray();
    }

    private static void WriteChunk(Stream stream, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> len = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(len, data.Length);
        stream.Write(len);
        stream.Write(type);
        stream.Write(data);
        var crc = Crc32(type, data);
        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        stream.Write(crcBytes);
    }

    private static uint Adler32(ReadOnlySpan<byte> data)
    {
        uint a = 1, b = 0;
        foreach (var x in data)
        {
            a = (a + x) % 65521;
            b = (b + a) % 65521;
        }

        return (b << 16) | a;
    }

    private static uint Crc32(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var x in type)
            crc = CrcTable[(crc ^ x) & 0xFF] ^ (crc >> 8);
        foreach (var x in data)
            crc = CrcTable[(crc ^ x) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFF;
    }

    private static readonly uint[] CrcTable = CreateCrcTable();

    private static uint[] CreateCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[n] = c;
        }

        return table;
    }
}
