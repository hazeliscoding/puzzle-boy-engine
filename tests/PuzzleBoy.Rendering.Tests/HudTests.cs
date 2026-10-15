using Microsoft.Xna.Framework;

namespace PuzzleBoy.Rendering.Tests;

public sealed class HudTests
{
    [Theory]
    [InlineData(1280, 720, 2f / 3, 0, 0)]
    [InlineData(1920, 1200, 1, 0, 60)]
    [InlineData(2560, 1080, 1, 320, 0)]
    public void The_layout_scales_uniformly_and_centres_in_the_window(int width, int height, float scale, float x, float y)
    {
        var fit = UiLayout.Fit(width, height);

        Assert.Equal(scale, fit.Scale, 5);
        Assert.Equal(new Vector2(x, y), fit.Offset);
    }

    [Theory]
    [InlineData(7, null, 7)]
    [InlineData(12, 1, 2)]
    [InlineData(20, 2, 0)]
    public void The_counter_leaves_the_tens_blank_below_10(int stage, int? tens, int ones)
    {
        Assert.Equal((tens, ones), Hud.Digits(stage));
    }

    [Theory]
    [InlineData(0f, 0f, 0f)]
    [InlineData(0.125f, 9f, 0.1875f)]
    [InlineData(0.25f, 24f, 0.5f)]
    [InlineData(0.5f, 0f, 0f)]
    public void The_arrows_bob_peaks_at_the_middle_key(float seconds, float offset, float glow)
    {
        var bob = Hud.Bob(seconds);

        Assert.Equal(offset, bob.Offset, 4);
        Assert.Equal(glow, bob.Glow, 4);
    }
}
