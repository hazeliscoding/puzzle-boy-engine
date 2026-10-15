using System.Buffers.Binary;
using Microsoft.Xna.Framework;

namespace PuzzleBoy.Nocturne.Tests;

// Handmade data in the layouts re/m3-jack.md records, so these need no game files.
public sealed class UnityFormatsTests
{
    private static readonly VertexChannel[] Layout =
    [
        new(0, 0, 0, 3), new(0, 12, 0, 3), default, default,
        new(1, 0, 0, 2), default, default, default, default, default, default, default,
        new(2, 0, 0, 2), new(2, 8, 10, 2),
    ];

    [Fact]
    public void Streams_are_interleaved_and_each_starts_16_byte_aligned()
    {
        Assert.Equal([(0, 24), (80, 8), (112, 16)], UnityFormats.Streams(Layout, 3));
    }

    [Fact]
    public void Channels_decode_from_their_stream_and_offset()
    {
        var data = new byte[160];
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(24 + 12 + 4), 7.5f);
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(80 + 8 + 4), 0.25f);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(112 + 16 + 8 + 4), 9);

        Assert.Equal(7.5f, UnityFormats.Read(data, Layout, UnityFormats.Normal, 3)![4]);
        Assert.Equal(0.25f, UnityFormats.Read(data, Layout, UnityFormats.Uv0, 3)![3]);
        Assert.Equal(9f, UnityFormats.Read(data, Layout, UnityFormats.BlendIndices, 3)![3]);
        Assert.Null(UnityFormats.Read(data, Layout, 2, 3));
    }

    [Fact]
    public void An_unread_vertex_format_throws()
    {
        VertexChannel[] halfs = [new(0, 0, 1, 3)];

        Assert.Throws<InvalidDataException>(() => UnityFormats.Read(new byte[16], halfs, UnityFormats.Position, 1));
    }

    [Fact]
    public void A_submesh_reads_its_own_indices()
    {
        byte[] buffer = [0, 0, 1, 0, 2, 0, 3, 0, 4, 0, 5, 0];

        Assert.Equal([3, 4, 5], UnityFormats.Indices(buffer, wide: false, firstByte: 6, count: 3));
    }

    [Fact]
    public void A_bind_pose_translation_moves_the_origin()
    {
        float[] e = [1, 0, 0, 2, 0, 1, 0, 3, 0, 0, 1, 4, 0, 0, 0, 1];

        Assert.Equal(new Vector3(2, 3, 4), Vector3.Transform(Vector3.Zero, UnityFormats.Matrix(e)));
    }

    [Theory]
    [InlineData("", 0u)]
    [InlineData("123456789", 0xCBF43926u)]
    public void Path_hashes_are_standard_crc32(string path, uint hash)
    {
        Assert.Equal(hash, UnityFormats.PathHash(path));
    }

    [Fact]
    public void Bindings_take_3_curves_for_position_and_scale_and_4_for_rotation()
    {
        var bindings = UnityFormats.Bind([(1, 1, 4), (2, 2, 4), (3, 3, 4)], 10);

        Assert.Equal([0, 3, 7], bindings.Select(b => b.FirstCurve));
        Assert.Throws<InvalidDataException>(() => UnityFormats.Bind([(1, 1, 4), (2, 2, 4), (3, 3, 4)], 11));
        Assert.Throws<InvalidDataException>(() => UnityFormats.Bind([(1, 4, 4)], 3));
        Assert.Throws<InvalidDataException>(() => UnityFormats.Bind([(1, 1, 23)], 1));
    }

    [Fact]
    public void Streamed_keys_hold_a_cubic_until_the_next_key()
    {
        var curves = AnimationCurves.Parse(
        [
            Word(float.MinValue), 2, 0, Word(0), Word(0), Word(0), Word(1), 1, Word(0), Word(0), Word(0), Word(5),
            Word(0.5f), 1, 0, Word(1), Word(2), Word(3), Word(4),
            Word(float.PositiveInfinity), 0,
        ], streamedCount: 2, denseCount: 0, constants: [8]);

        Assert.Equal(3, curves.Count);
        Assert.Equal(1f, curves.Value(0, 0.25f));
        Assert.Equal(4f, curves.Value(0, 0.5f));
        Assert.Equal(6.125f, curves.Value(0, 1f));
        Assert.Equal(5f, curves.Value(1, 1f));
        Assert.Equal(8f, curves.Value(2, 1f));
    }

    [Fact]
    public void A_key_a_float_step_after_another_time_applies_only_from_its_own_time()
    {
        var later = BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(0.1f) + 1);
        var curves = AnimationCurves.Parse(
        [
            Word(float.MinValue), 1, 0, Word(0), Word(0), Word(0), Word(1),
            Word(later), 1, 0, Word(0), Word(0), Word(0), Word(2),
            Word(float.PositiveInfinity), 0,
        ], streamedCount: 1, denseCount: 0, constants: []);

        Assert.Equal(1f, curves.Value(0, 0.1f));
        Assert.Equal(2f, curves.Value(0, later));
    }

    [Fact]
    public void Dense_curves_throw()
    {
        Assert.Throws<InvalidDataException>(() => AnimationCurves.Parse([], 0, 1, []));
    }

    private static uint Word(float value) => BitConverter.SingleToUInt32Bits(value);
}
