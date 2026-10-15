namespace PuzzleBoy.Nocturne.Tests;

public sealed class TextureDecoderTests
{
    // One DXT1 block: red (0xF800) and blue (0x001F) endpoints, with the 2-bit indices given row by row.
    private static byte[] Block(ushort c0, ushort c1, uint indices) =>
        [(byte)c0, (byte)(c0 >> 8), (byte)c1, (byte)(c1 >> 8), (byte)indices, (byte)(indices >> 8), (byte)(indices >> 16), (byte)(indices >> 24)];

    [Fact]
    public void A_four_color_block_blends_two_thirds_and_one_third()
    {
        // Pixels 0-3 use indices 0, 1, 2, 3.
        var rgba = TextureDecoder.Dxt1(Block(0xF800, 0x001F, 0b11_10_01_00), 4, 4);

        Assert.Equal([255, 0, 0, 255], rgba[0..4]);
        Assert.Equal([0, 0, 255, 255], rgba[4..8]);
        Assert.Equal([170, 0, 85, 255], rgba[8..12]);
        Assert.Equal([85, 0, 170, 255], rgba[12..16]);
    }

    [Fact]
    public void A_three_color_block_uses_index_3_for_transparent_black()
    {
        var rgba = TextureDecoder.Dxt1(Block(0x001F, 0xF800, 0b11_10), 4, 4);

        Assert.Equal([127, 0, 127, 255], rgba[0..4]);
        Assert.Equal([0, 0, 0, 0], rgba[4..8]);
    }

    [Fact]
    public void A_level_smaller_than_a_block_keeps_only_its_pixels()
    {
        var rgba = TextureDecoder.Dxt1(Block(0xF800, 0x001F, 0), 2, 1);

        Assert.Equal(8, rgba.Length);
        Assert.Equal(8, TextureDecoder.Dxt1Size(2, 1));
    }

    [Fact]
    public void Rgb24_gets_full_alpha()
    {
        Assert.Equal([1, 2, 3, 255, 4, 5, 6, 255], TextureDecoder.Rgb24([1, 2, 3, 4, 5, 6], 2, 1));
    }
}
