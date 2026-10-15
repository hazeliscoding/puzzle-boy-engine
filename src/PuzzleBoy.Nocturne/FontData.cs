using Microsoft.Xna.Framework;

namespace PuzzleBoy.Nocturne;

/// <summary>A glyph of the TextMeshPro font, in font units at the font's point size (1 unit = 1 atlas texel there).</summary>
/// <param name="Width">The metric box's width.</param>
/// <param name="Height">The metric box's height.</param>
/// <param name="BearingX">From the pen to the box's left edge.</param>
/// <param name="BearingY">From the baseline up to the box's top.</param>
/// <param name="Advance">How far the pen moves.</param>
/// <param name="Rect">The glyph's rect in the atlas, in texels from the bottom-left as Unity stores it.</param>
public readonly record struct FontGlyph(float Width, float Height, float BearingX, float BearingY, float Advance, Rectangle Rect);

/// <summary>The game's TextMeshPro SDF font: glyphs and the distance-field atlas, one byte per texel, rows bottom-up.</summary>
/// <param name="PointSize">The size the glyph metrics are given at.</param>
/// <param name="Ascent">The ascent line, in font units.</param>
/// <param name="Descent">The descent line, in font units (negative).</param>
/// <param name="Glyphs">Glyphs by character.</param>
/// <param name="AtlasWidth">The atlas's width in texels.</param>
/// <param name="AtlasHeight">The atlas's height in texels.</param>
/// <param name="Atlas">The atlas: 0.5 + distance / 22 per texel as a byte, positive inside.</param>
public sealed record FontData(float PointSize, float Ascent, float Descent, IReadOnlyDictionary<char, FontGlyph> Glyphs, int AtlasWidth, int AtlasHeight, byte[] Atlas);

/// <summary>A TextMeshPro distance-field material: colours and the floats that place its edges.</summary>
/// <param name="Face">The face colour, straight alpha.</param>
/// <param name="Outline">The outline colour.</param>
/// <param name="Underlay">The underlay colour; its alpha is the underlay's strength.</param>
/// <param name="FaceDilate">TMP's <c>_FaceDilate</c>.</param>
/// <param name="OutlineWidth">TMP's <c>_OutlineWidth</c>.</param>
/// <param name="UnderlayDilate">TMP's <c>_UnderlayDilate</c>.</param>
/// <param name="ScaleRatioA">TMP's <c>_ScaleRatioA</c>.</param>
/// <param name="ScaleRatioC">TMP's <c>_ScaleRatioC</c>.</param>
/// <param name="GradientScale">TMP's <c>_GradientScale</c>: the atlas's distance range in texels.</param>
public sealed record TextMaterial(
    Vector4 Face, Vector4 Outline, Vector4 Underlay, float FaceDilate, float OutlineWidth, float UnderlayDilate,
    float ScaleRatioA, float ScaleRatioC, float GradientScale)
{
    // shortcut: FIT. The footage's text is slightly thinner than these floats give; raising the face edge by 0.035
    // matches it best at 720p (re/m3-ui.md, 3.2). Revisit with a 1080p capture.
    private const float FaceFit = 0.035f;

    private float Weight => FaceDilate * ScaleRatioA / 2;

    /// <summary>The atlas value where the face's coverage is half, as the material sets it (no fit).</summary>
    public float FaceEdge => 0.5f - Weight - (OutlineWidth * ScaleRatioA / 2);

    /// <summary>Where the colour turns from face to outline.</summary>
    public float ColourEdge => 0.5f - Weight + (OutlineWidth * ScaleRatioA / 2);

    /// <summary>Where the underlay's coverage is half.</summary>
    public float UnderlayEdge => 0.5f - Weight - (UnderlayDilate * ScaleRatioC / 2);

    /// <summary>The face edge the port draws with, thinned to match the footage.</summary>
    public float DrawnFaceEdge => FaceEdge + FaceFit;
}
