using System.Buffers.Binary;
using Microsoft.Xna.Framework;

namespace PuzzleBoy.Nocturne;

/// <summary>A vertex channel of a Unity 2019 mesh: its stream, its byte offset in a vertex, its format and its component count.</summary>
/// <param name="Stream">The vertex stream that holds it.</param>
/// <param name="Offset">Its byte offset inside one vertex of that stream.</param>
/// <param name="Format">Unity's vertex format: 0 is float32 and 10 is uint32, the only two the port reads.</param>
/// <param name="Dimension">Components per vertex; 0 means the mesh doesn't have this channel.</param>
internal readonly record struct VertexChannel(int Stream, int Offset, int Format, int Dimension);

/// <summary>A Transform binding of an animation clip: the node's path hash, what it animates, and its first curve.</summary>
/// <param name="Path">The CRC-32 of the node's path below the animated object (<see cref="UnityFormats.PathHash"/>).</param>
/// <param name="Attribute">1 position, 2 rotation, 3 scale.</param>
/// <param name="FirstCurve">The first of its 3 (position, scale) or 4 (rotation) curves.</param>
internal readonly record struct ClipBinding(uint Path, int Attribute, int FirstCurve);

/// <summary>
/// Decoders for the Unity 2019 mesh and animation data Jack-o'-Lantern is made of (re/m3-jack.md has the layouts).
/// They work on plain arrays so tests can feed them made-up numbers; <see cref="GameInstall"/> only reads fields.
/// </summary>
internal static class UnityFormats
{
    /// <summary>Unity's fixed channel slots in 2019.</summary>
    public const int Position = 0, Normal = 1, Uv0 = 4, BlendWeight = 12, BlendIndices = 13;

    private static readonly uint[] Crc = Enumerable.Range(0, 256).Select(n =>
    {
        var c = (uint)n;
        for (var k = 0; k < 8; k++)
        {
            c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
        }

        return c;
    }).ToArray();

    /// <summary>
    /// Each stream's start and stride. A stream's channels are interleaved per vertex, and each stream starts where the
    /// one before ends, rounded up to 16 bytes.
    /// </summary>
    public static (int Start, int Stride)[] Streams(IReadOnlyList<VertexChannel> channels, int vertexCount)
    {
        var count = channels.Where(c => c.Dimension > 0).Select(c => c.Stream + 1).DefaultIfEmpty(0).Max();
        var streams = new (int Start, int Stride)[count];
        foreach (var c in channels.Where(c => c.Dimension > 0))
        {
            streams[c.Stream].Stride += 4 * c.Dimension;
        }

        var at = 0;
        for (var s = 0; s < count; s++)
        {
            streams[s].Start = at;
            at = (at + (streams[s].Stride * vertexCount) + 15) & ~15;
        }

        return streams;
    }

    /// <summary>
    /// One channel's values, <see cref="VertexChannel.Dimension"/> per vertex, or null when the mesh lacks it.
    /// Throws <see cref="InvalidDataException"/> for a format other than float32 or uint32.
    /// </summary>
    public static float[]? Read(ReadOnlySpan<byte> data, IReadOnlyList<VertexChannel> channels, int channel, int vertexCount)
    {
        if (channel >= channels.Count || channels[channel] is not { Dimension: > 0 } c)
        {
            return null;
        }

        if (c.Format is not (0 or 10))
        {
            throw new InvalidDataException($"Vertex channel {channel} has format {c.Format}, which the port doesn't read.");
        }

        var (start, stride) = Streams(channels, vertexCount)[c.Stream];
        var values = new float[vertexCount * c.Dimension];
        for (var v = 0; v < vertexCount; v++)
        {
            for (var i = 0; i < c.Dimension; i++)
            {
                var at = data[(start + (v * stride) + c.Offset + (4 * i))..];
                values[(v * c.Dimension) + i] = c.Format == 0 ? BinaryPrimitives.ReadSingleLittleEndian(at) : BinaryPrimitives.ReadUInt32LittleEndian(at);
            }
        }

        return values;
    }

    /// <summary>A submesh's indices: <paramref name="count"/> of them from byte <paramref name="firstByte"/>, 16-bit unless <paramref name="wide"/>.</summary>
    public static int[] Indices(ReadOnlySpan<byte> buffer, bool wide, int firstByte, int count)
    {
        var indices = new int[count];
        for (var i = 0; i < count; i++)
        {
            indices[i] = wide
                ? (int)BinaryPrimitives.ReadUInt32LittleEndian(buffer[(firstByte + (4 * i))..])
                : BinaryPrimitives.ReadUInt16LittleEndian(buffer[(firstByte + (2 * i))..]);
        }

        return indices;
    }

    /// <summary>
    /// A Unity Matrix4x4f, given as e00, e01, e02, e03, e10 … e33 (row, column, for column vectors), as a MonoGame
    /// matrix for row vectors: the transpose, with Unity's translation (e03, e13, e23) in M41–M43.
    /// </summary>
    public static Matrix Matrix(ReadOnlySpan<float> e) =>
        new(e[0], e[4], e[8], e[12], e[1], e[5], e[9], e[13], e[2], e[6], e[10], e[14], e[3], e[7], e[11], e[15]);

    /// <summary>Unity's path hash: the standard CRC-32 of the path's UTF-8 bytes.</summary>
    public static uint PathHash(string path)
    {
        var crc = 0xFFFFFFFF;
        foreach (var b in System.Text.Encoding.UTF8.GetBytes(path))
        {
            crc = Crc[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return ~crc;
    }

    /// <summary>
    /// Assigns curves to bindings in order: a position or scale takes 3, a rotation 4. Throws
    /// <see cref="InvalidDataException"/> for any other binding, or when they don't cover exactly <paramref name="curveCount"/>.
    /// </summary>
    /// <param name="bindings">Each binding's path hash, attribute and type id (4 is Transform).</param>
    /// <param name="curveCount">The clip's streamed, dense and constant curves together.</param>
    public static ClipBinding[] Bind(IEnumerable<(uint Path, int Attribute, int TypeId)> bindings, int curveCount)
    {
        var result = new List<ClipBinding>();
        var curve = 0;
        foreach (var (path, attribute, typeId) in bindings)
        {
            var size = (typeId, attribute) switch
            {
                (4, 1 or 3) => 3,
                (4, 2) => 4,
                _ => throw new InvalidDataException($"An animation binding of type {typeId}, attribute {attribute}, which the port doesn't read."),
            };
            result.Add(new ClipBinding(path, attribute, curve));
            curve += size;
        }

        return curve == curveCount ? [.. result] : throw new InvalidDataException($"Animation bindings cover {curve} curves, but the clip has {curveCount}.");
    }
}

/// <summary>
/// A generic AnimationClip's curves: the streamed ones first, then the constants. A streamed key holds from its time
/// until the curve's next key, as the cubic ((a·dt + b)·dt + c)·dt + d in seconds since the key.
/// </summary>
public sealed class AnimationCurves
{
    private readonly (float Time, float A, float B, float C, float D)[][] _streamed;
    private readonly float[] _constants;

    private AnimationCurves((float, float, float, float, float)[][] streamed, float[] constants) => (_streamed, _constants) = (streamed, constants);

    /// <summary>The number of curves.</summary>
    public int Count => _streamed.Length + _constants.Length;

    /// <summary>
    /// Reads the streamed words: frames of a float time and a key count, each key a curve index and four floats.
    /// Throws <see cref="InvalidDataException"/> when the clip has dense curves, which the port doesn't read.
    /// </summary>
    public static AnimationCurves Parse(ReadOnlySpan<uint> streamed, int streamedCount, int denseCount, float[] constants)
    {
        if (denseCount > 0)
        {
            throw new InvalidDataException("An animation clip with dense curves, which the port doesn't read.");
        }

        var keys = Enumerable.Range(0, streamedCount).Select(_ => new List<(float, float, float, float, float)>()).ToArray();
        for (var i = 0; i < streamed.Length;)
        {
            var time = BitConverter.UInt32BitsToSingle(streamed[i]);
            var count = (int)streamed[i + 1];
            i += 2;
            for (var k = 0; k < count; k++, i += 5)
            {
                keys[streamed[i]].Add((time, F(streamed[i + 1]), F(streamed[i + 2]), F(streamed[i + 3]), F(streamed[i + 4])));
            }
        }

        return new AnimationCurves([.. keys.Select(k => k.ToArray())], constants);

        static float F(uint word) => BitConverter.UInt32BitsToSingle(word);
    }

    /// <summary>Curve <paramref name="curve"/> at <paramref name="time"/> seconds.</summary>
    public float Value(int curve, float time)
    {
        if (curve >= _streamed.Length)
        {
            return _constants[curve - _streamed.Length];
        }

        // Find the last key at or before the time. Frame times aren't exact multiples of 1/30, so search by time.
        var keys = _streamed[curve];
        var (lo, hi) = (0, keys.Length - 1);
        while (lo < hi)
        {
            var mid = (lo + hi + 1) / 2;
            (lo, hi) = keys[mid].Time <= time ? (mid, hi) : (lo, mid - 1);
        }

        var (at, a, b, c, d) = keys[lo];
        if (at == float.MinValue)
        {
            return d;
        }

        var dt = time - at;
        return (((((a * dt) + b) * dt) + c) * dt) + d;
    }
}
