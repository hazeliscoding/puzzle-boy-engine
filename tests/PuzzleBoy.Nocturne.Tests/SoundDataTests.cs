namespace PuzzleBoy.Nocturne.Tests;

public sealed class SoundDataTests
{
    [Fact]
    public void Float_samples_become_16_bit_little_endian_and_loud_ones_clamp()
    {
        Assert.Equal([0, 0, 0xFF, 0x7F, 0x01, 0x80, 0xFF, 0x7F, 0x00, 0x80], SoundData.Pcm16([0f, 1f, -1f, 2f, -3f]));
    }
}
