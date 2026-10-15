using System.Buffers.Binary;

namespace PuzzleBoy.Nocturne;

/// <summary>
/// Decodes Unity texture data to RGBA bytes, 4 per pixel, keeping Unity's row order (bottom row first).
/// Decoding on the CPU keeps every platform the same, whatever compressed formats its GPU takes.
/// </summary>
internal static class TextureDecoder
{
    /// <summary>The bytes one mip level of a DXT1 texture takes: 8 per 4x4 block, partial blocks rounded up.</summary>
    public static int Dxt1Size(int width, int height) => Math.Max(1, (width + 3) / 4) * Math.Max(1, (height + 3) / 4) * 8;

    /// <summary>Decodes one DXT1 (BC1) mip level. A block whose first color isn't greater uses index 3 for transparent black.</summary>
    public static byte[] Dxt1(ReadOnlySpan<byte> data, int width, int height)
    {
        var rgba = new byte[width * height * 4];
        var at = 0;
        Span<uint> palette = stackalloc uint[4];
        for (var by = 0; by < height; by += 4)
        {
            for (var bx = 0; bx < width; bx += 4, at += 8)
            {
                var c0 = BinaryPrimitives.ReadUInt16LittleEndian(data[at..]);
                var c1 = BinaryPrimitives.ReadUInt16LittleEndian(data[(at + 2)..]);
                var bits = BinaryPrimitives.ReadUInt32LittleEndian(data[(at + 4)..]);
                var (a, b) = (Rgb565(c0), Rgb565(c1));
                palette[0] = Pack(a, 255);
                palette[1] = Pack(b, 255);
                palette[2] = c0 > c1 ? Pack(Mix(a, b, 2, 1, 3), 255) : Pack(Mix(a, b, 1, 1, 2), 255);
                palette[3] = c0 > c1 ? Pack(Mix(a, b, 1, 2, 3), 255) : 0;
                for (var i = 0; i < 16; i++)
                {
                    var (x, y) = (bx + (i % 4), by + (i / 4));
                    if (x < width && y < height)
                    {
                        BinaryPrimitives.WriteUInt32LittleEndian(rgba.AsSpan(4 * ((y * width) + x)), palette[(int)((bits >> (2 * i)) & 3)]);
                    }
                }
            }
        }

        return rgba;
    }

    /// <summary>Expands RGB24 to RGBA with full alpha.</summary>
    public static byte[] Rgb24(ReadOnlySpan<byte> data, int width, int height)
    {
        var rgba = new byte[width * height * 4];
        for (var i = 0; i < width * height; i++)
        {
            data.Slice(3 * i, 3).CopyTo(rgba.AsSpan(4 * i));
            rgba[(4 * i) + 3] = 255;
        }

        return rgba;
    }

    private static (int R, int G, int B) Rgb565(ushort c) => ((c >> 11) * 255 / 31, ((c >> 5) & 63) * 255 / 63, (c & 31) * 255 / 31);

    private static (int R, int G, int B) Mix((int R, int G, int B) a, (int R, int G, int B) b, int wa, int wb, int d) =>
        (((a.R * wa) + (b.R * wb)) / d, ((a.G * wa) + (b.G * wb)) / d, ((a.B * wa) + (b.B * wb)) / d);

    /// <summary>A pixel as little-endian RGBA bytes.</summary>
    private static uint Pack((int R, int G, int B) c, int alpha) => (uint)(c.R | (c.G << 8) | (c.B << 16) | (alpha << 24));
}
