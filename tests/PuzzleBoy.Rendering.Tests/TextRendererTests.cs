using Microsoft.Xna.Framework;
using PuzzleBoy.Nocturne;

namespace PuzzleBoy.Rendering.Tests;

// Made-up fonts and materials, so these need no game files (re/m3-ui.md, section 9).
public sealed class TextRendererTests
{
    private static readonly TextMaterial Plain = new(Vector4.One, new Vector4(0, 0, 1, 1), new Vector4(0, 0, 1, 0.5f), 0, 0, 0, 1, 1, 11);

    private static FontData Font(byte fill = 255) => new(100, 80, -20,
        new Dictionary<char, FontGlyph>
        {
            ['a'] = new(10, 10, 0, 10, 40, new Rectangle(20, 20, 10, 10)),
            ['b'] = new(10, 10, 0, 10, 60, new Rectangle(20, 20, 10, 10)),
            [' '] = new(0, 0, 0, 0, 30, new Rectangle(0, 0, 0, 0)),
        },
        64, 64, Enumerable.Repeat(fill, 64 * 64).ToArray());

    [Fact]
    public void A_texel_on_the_face_edge_is_half_covered_and_full_coverage_is_a_ramp_away()
    {
        Assert.Equal(0.5f, TextRenderer.Coverage(0.4f, 0.4f, 10));
        Assert.Equal(1f, TextRenderer.Coverage(0.4f + 0.05f, 0.4f, 10));
        Assert.Equal(0f, TextRenderer.Coverage(0.4f - 0.05f, 0.4f, 10));
    }

    [Fact]
    public void The_pen_advances_by_size_over_point_size()
    {
        Assert.Equal(65f, TextRenderer.Advance(Font(), "ab ", 50));
    }

    [Fact]
    public void Middle_alignment_puts_the_baseline_below_the_box_centre()
    {
        Assert.Equal(115f, TextRenderer.MiddleBaseline(Font(), 100, 50));
    }

    [Fact]
    public void Colour_tags_take_no_width_and_switch_the_material_of_what_follows()
    {
        var runs = TextRenderer.Runs("a<COLOR 1>b<COLOR 0> c");

        Assert.Equal("ab c", new string([.. runs.Select(r => r.Char)]));
        Assert.Equal([0, 1, 0, 0], runs.Select(r => r.Material));
    }

    [Fact]
    public void A_glyph_inside_the_face_bakes_to_the_face_colour()
    {
        var baked = TextRenderer.Bake(Font(), [Plain], [('a', 0)], 100, 0, 50, (1f, Vector2.Zero));

        // The glyph box spans pen 0..10 and baseline-10..baseline; its centre pixel is fully covered and white.
        var centre = 4 * (((45 - baked.Y) * baked.Width) + (5 - baked.X));
        Assert.Equal([255, 255, 255, 255], baked.Rgba[centre..(centre + 4)]);
    }

    [Fact]
    public void Nothing_to_draw_bakes_an_empty_image()
    {
        Assert.Equal(0, TextRenderer.Bake(Font(), [Plain], [(' ', 0)], 50, 0, 50, (1f, Vector2.Zero)).Width);
    }

    [Fact]
    public void The_bar_lays_out_sets_from_the_right_with_a_gap()
    {
        Assert.Equal([740f, 874f], Hud.BarLayout([(1, 50f), (2, 30f)], right: 1000, gap: 36, icon: 48));
    }

    [Theory]
    [InlineData(0, 0f)]
    [InlineData(15, 1f)]
    [InlineData(30, 1f)]
    public void The_bar_fades_in_with_a_smoothstep_over_15_ticks(int ticks, float opacity)
    {
        Assert.Equal(opacity, Hud.BarOpacity(ticks), 4);
    }
}
