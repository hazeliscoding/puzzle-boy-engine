using Microsoft.Xna.Framework;

namespace PuzzleBoy.Nocturne.Tests;

// A made-up material, so this needs no game files (re/m3-ui.md, section 9).
public sealed class TextMaterialTests
{
    private static readonly TextMaterial Plain = new(Vector4.One, new Vector4(0, 0, 1, 1), new Vector4(0, 0, 1, 0.5f), 0, 0, 0, 1, 1, 11);

    [Fact]
    public void A_materials_edges_come_from_its_floats()
    {
        var material = Plain with { FaceDilate = 0.2f, OutlineWidth = 0.2f, UnderlayDilate = 0.4f };

        Assert.Equal(0.3f, material.FaceEdge, 5);
        Assert.Equal(0.5f, material.ColourEdge, 5);
        Assert.Equal(0.2f, material.UnderlayEdge, 5);
    }
}
