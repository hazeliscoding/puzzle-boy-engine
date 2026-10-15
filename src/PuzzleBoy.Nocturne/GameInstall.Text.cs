using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Microsoft.Xna.Framework;

namespace PuzzleBoy.Nocturne;

/// <summary>The game's font, its text materials and the English strings Puzzle Boy shows.</summary>
/// <param name="Font">The TextMeshPro font, <c>FOT-MatisseProN-B SDF</c>.</param>
/// <param name="Materials">Its materials by name, such as <c>MsgFont0</c> or <c>TMC22</c>.</param>
/// <param name="Strings">Strings by id, such as <c>PBMESSAGE_L0002</c>, <c>PBHELP_L0046</c> or <c>GUIDE_L0012</c>.</param>
public sealed record TextData(FontData Font, IReadOnlyDictionary<string, TextMaterial> Materials, IReadOnlyDictionary<string, string> Strings);

/// <content>Reading the font and the text.</content>
public sealed partial class GameInstall
{
    private const string FontName = "FOT-MatisseProN-B SDF";

    /// <summary>
    /// The font and materials from <c>font-en</c> and the strings of <c>PBMESSAGE_EN</c>, <c>PUZZLEBOY_EN</c>
    /// (<c>pazzleboy_en</c>) and <c>GUIDE_EN</c> (<c>common_en</c>). Throws <see cref="InvalidDataException"/> when
    /// something is missing or the atlas isn't one byte per texel.
    /// </summary>
    // shortcut: English only; other languages need their bundles and wrapping, since the boxes were sized for Japanese.
    public TextData ReadText()
    {
        var strings = new Dictionary<string, string>();
        foreach (var (bundle, table) in new[] { ("pazzleboy_en", "PBMESSAGE_EN"), ("pazzleboy_en", "PUZZLEBOY_EN"), ("common_en", "GUIDE_EN") })
        {
            ReadAssets(bundle, (manager, file, _) =>
            {
                var t = Behaviours(manager, file).FirstOrDefault(b => b["m_Name"].AsString == table)
                    ?? throw new InvalidDataException($"There's no text table {table} in {bundle}.");
                foreach (var entry in t["_eventEMsgData.Array"].Children)
                {
                    strings[entry["msgID"].AsString] = entry["msgStr"].AsString;
                }
            });
        }

        FontData font = null!;
        var materials = new Dictionary<string, TextMaterial>();
        ReadAssets("font-en", (manager, file, bundle) =>
        {
            var f = Behaviours(manager, file).FirstOrDefault(b => b["m_Name"].AsString == FontName)
                ?? throw new InvalidDataException($"There's no font {FontName} in font-en.");
            var glyphs = f["m_GlyphTable.Array"].Children.ToDictionary(g => g["m_Index"].AsUInt);
            var characters = new Dictionary<char, FontGlyph>();
            foreach (var c in f["m_CharacterTable.Array"].Children)
            {
                var g = glyphs[c["m_GlyphIndex"].AsUInt];
                var (m, r) = (g["m_Metrics"], g["m_GlyphRect"]);
                var unicode = c["m_Unicode"].AsUInt;
                if (unicode <= char.MaxValue)
                {
                    characters[(char)unicode] = new FontGlyph(m["m_Width"].AsFloat, m["m_Height"].AsFloat, m["m_HorizontalBearingX"].AsFloat,
                        m["m_HorizontalBearingY"].AsFloat, m["m_HorizontalAdvance"].AsFloat, new Rectangle(r["m_X"].AsInt, r["m_Y"].AsInt, r["m_Width"].AsInt, r["m_Height"].AsInt));
                }
            }

            var atlas = manager.GetBaseField(file, file.file.GetAssetInfo(f["m_AtlasTextures.Array"].Children[0]["m_PathID"].AsLong));
            if (atlas["m_TextureFormat"].AsInt != 1)
            {
                throw new InvalidDataException($"The font atlas has format {atlas["m_TextureFormat"].AsInt}; the port reads Alpha8 only.");
            }

            var stream = atlas["m_StreamData"];
            var data = stream["size"].AsUInt > 0
                ? ReadEntry(bundle, stream["path"].AsString, stream["offset"].AsULong, stream["size"].AsUInt)
                : atlas["image data"].AsByteArray;
            var face = f["m_FaceInfo"];
            font = new FontData(face["m_PointSize"].AsFloat, face["m_AscentLine"].AsFloat, face["m_DescentLine"].AsFloat,
                characters, atlas["m_Width"].AsInt, atlas["m_Height"].AsInt, data);

            foreach (var info in file.file.GetAssetsOfType(AssetClassID.Material))
            {
                var material = manager.GetBaseField(file, info);
                var properties = material["m_SavedProperties"];
                var colours = properties["m_Colors.Array"].Children.ToDictionary(e => e["first"].AsString, e => new Vector4(e["second.r"].AsFloat, e["second.g"].AsFloat, e["second.b"].AsFloat, e["second.a"].AsFloat));
                var floats = properties["m_Floats.Array"].Children.ToDictionary(e => e["first"].AsString, e => e["second"].AsFloat);
                if (colours.ContainsKey("_FaceColor") && floats.ContainsKey("_GradientScale"))
                {
                    materials.TryAdd(material["m_Name"].AsString, new TextMaterial(colours["_FaceColor"], colours["_OutlineColor"], colours["_UnderlayColor"],
                        floats["_FaceDilate"], floats["_OutlineWidth"], floats["_UnderlayDilate"], floats["_ScaleRatioA"], floats["_ScaleRatioC"], floats["_GradientScale"]));
                }
            }
        });

        return new TextData(font, materials, strings);
    }

    private void ReadAssets(string bundle, Action<AssetsManager, AssetsFileInstance, BundleFileInstance> read)
    {
        var manager = new AssetsManager();
        try
        {
            var bundleFile = manager.LoadBundleFile(BundlePath(Directory, bundle), true);
            read(manager, manager.LoadAssetsFileFromBundle(bundleFile, 0, false), bundleFile);
        }
        finally
        {
            manager.UnloadAll();
        }
    }

    private static IEnumerable<AssetTypeValueField> Behaviours(AssetsManager manager, AssetsFileInstance file) =>
        file.file.GetAssetsOfType(AssetClassID.MonoBehaviour).Select(info => manager.GetBaseField(file, info));
}
