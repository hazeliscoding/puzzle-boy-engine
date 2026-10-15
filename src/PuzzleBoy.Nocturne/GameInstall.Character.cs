using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Microsoft.Xna.Framework;

namespace PuzzleBoy.Nocturne;

/// <summary>A node of the character's prefab, with its stored local transform.</summary>
/// <param name="Name">The GameObject's name.</param>
/// <param name="Parent">The parent's index in <see cref="CharacterData.Nodes"/>, or -1 for the root.</param>
/// <param name="Position">Local position, in Unity's axes.</param>
/// <param name="Rotation">Local rotation, a Unity quaternion (MonoGame reads the same numbers the same way).</param>
/// <param name="Scale">Local scale.</param>
public sealed record CharacterNode(string Name, int Parent, Vector3 Position, Quaternion Rotation, Vector3 Scale);

/// <summary>One submesh and the material it's drawn with.</summary>
/// <param name="Indices">Triangle indices into the mesh's vertices.</param>
/// <param name="Texture">The material's main texture, by name in <see cref="CharacterData.Textures"/>.</param>
/// <param name="CullBack">True when the material draws front faces only (Unity's <c>_Cull</c> 2).</param>
/// <param name="Emissive">True when the material lights itself (<c>_EmiPower</c> above 0), like Jack's eyes.</param>
public sealed record CharacterPart(int[] Indices, string Texture, bool CullBack, bool Emissive);

/// <summary>A mesh on a node, rigid or skinned, in its renderer's local space.</summary>
/// <param name="Node">The renderer's node.</param>
/// <param name="Positions">Vertex positions.</param>
/// <param name="Normals">Vertex normals.</param>
/// <param name="Uvs">Texture coordinates, Unity's (v up from the image's bottom row).</param>
/// <param name="Bones">For a skinned mesh, the nodes its blend indices name; empty for a rigid one.</param>
/// <param name="BindPoses">One per bone, as MonoGame matrices.</param>
/// <param name="Influences">Per vertex, its bones (indices into <paramref name="Bones"/>) and weights.</param>
/// <param name="Parts">The submeshes.</param>
/// <param name="Shadow">True for the shadow quad, which subtracts from what's under it.</param>
public sealed record CharacterMesh(
    int Node, Vector3[] Positions, Vector3[] Normals, Vector2[] Uvs, int[] Bones, Matrix[] BindPoses,
    (int Bone, float Weight)[][] Influences, CharacterPart[] Parts, bool Shadow);

/// <summary>An animated node property: which node, 1 position, 2 rotation or 3 scale, and its first curve.</summary>
/// <param name="Node">The animated node's index.</param>
/// <param name="Attribute">1 position, 2 rotation, 3 scale.</param>
/// <param name="FirstCurve">Its first curve in <see cref="CharacterData.Curves"/>.</param>
public readonly record struct NodeBinding(int Node, int Attribute, int FirstCurve);

/// <summary>Everything the port draws Jack-o'-Lantern with (re/m3-jack.md), read from the player's install.</summary>
/// <param name="Nodes">The prefab's nodes, parents before children.</param>
/// <param name="Placed">The node the game places on the board each tick (<c>devil_0x3a</c>, carrying the Animator).</param>
/// <param name="Fire">The lantern's hook point, where the glow sits (<c>h_fire</c>).</param>
/// <param name="Meshes">The rigid and skinned meshes.</param>
/// <param name="Textures">The materials' textures by name.</param>
/// <param name="Curves">The idle clip's curves.</param>
/// <param name="Bindings">Which node property each group of curves drives.</param>
/// <param name="LoopTicks">The idle loop's length in ticks (the clip's length times 30).</param>
/// <param name="Ramp">The toon ramp texture, <c>tone2</c> from the <c>common</c> bundle.</param>
public sealed record CharacterData(
    CharacterNode[] Nodes, int Placed, int Fire, CharacterMesh[] Meshes, IReadOnlyDictionary<string, TextureData> Textures,
    AnimationCurves Curves, NodeBinding[] Bindings, int LoopTicks, TextureData Ramp);

/// <content>Reading the character model.</content>
public sealed partial class GameInstall
{
    // Unity's built-in Quad, which the shadow uses and no bundle holds: 1 x 1 in the xy plane, facing -z.
    private static readonly Vector3[] QuadPositions = [new(-0.5f, -0.5f, 0), new(0.5f, -0.5f, 0), new(-0.5f, 0.5f, 0), new(0.5f, 0.5f, 0)];
    private static readonly Vector2[] QuadUvs = [new(0, 0), new(1, 0), new(0, 1), new(1, 1)];

    /// <summary>
    /// Jack-o'-Lantern from the <c>dvl0x150</c> bundle: the <c>devil_0x150</c> prefab's nodes, meshes, materials and
    /// textures, and its Animator's default clip. Throws <see cref="InvalidDataException"/> when the data isn't the shape
    /// re/m3-jack.md records.
    /// </summary>
    /// <param name="ramp">The toon ramp, <c>tone2</c> from the <c>common</c> bundle, read with that bundle's other textures.</param>
    public CharacterData ReadCharacter(TextureData ramp)
    {
        var manager = new AssetsManager();
        try
        {
            var bundle = manager.LoadBundleFile(BundlePath(Directory, "dvl0x150"), true);
            var file = manager.LoadAssetsFileFromBundle(bundle, 0, false);
            AssetTypeValueField Get(AssetTypeValueField pointer) => manager.GetBaseField(file, file.file.GetAssetInfo(pointer["m_PathID"].AsLong));
            AssetTypeValueField? Component(AssetTypeValueField gameObject, AssetClassID type) => gameObject["m_Component.Array"].Children
                .Select(c => c["component"])
                .Where(c => file.file.GetAssetInfo(c["m_PathID"].AsLong).TypeId == (int)type)
                .Select(Get)
                .FirstOrDefault();

            // The bundle holds nine FBX imports with copies of every mesh, so start from the prefab: the root GameObject named devil_0x150.
            var root = file.file.GetAssetsOfType(AssetClassID.GameObject)
                .Select(info => manager.GetBaseField(file, info))
                .Where(go => go["m_Name"].AsString == "devil_0x150")
                .Select(go => Component(go, AssetClassID.Transform)!)
                .FirstOrDefault(t => t["m_Father.m_PathID"].AsLong == 0)
                ?? throw new InvalidDataException("There's no devil_0x150 prefab in dvl0x150.");

            var nodes = new List<CharacterNode>();
            var gameObjects = new List<AssetTypeValueField>();
            var byTransform = new Dictionary<long, int>();
            void Walk(AssetTypeValueField transform, long id, int parent)
            {
                var go = Get(transform["m_GameObject"]);
                byTransform[id] = nodes.Count;
                nodes.Add(new CharacterNode(go["m_Name"].AsString, parent, Vector(transform["m_LocalPosition"]), Rotation(transform["m_LocalRotation"]), Vector(transform["m_LocalScale"])));
                gameObjects.Add(go);
                var self = nodes.Count - 1;
                foreach (var child in transform["m_Children.Array"].Children)
                {
                    Walk(Get(child), child["m_PathID"].AsLong, self);
                }
            }

            var rootGo = file.file.GetAssetsOfType(AssetClassID.Transform).First(info => manager.GetBaseField(file, info)["m_GameObject.m_PathID"].AsLong == root["m_GameObject.m_PathID"].AsLong);
            Walk(root, rootGo.PathId, -1);

            var textures = new Dictionary<string, TextureData>();
            var meshes = new List<CharacterMesh>();
            for (var n = 0; n < nodes.Count; n++)
            {
                var skinned = Component(gameObjects[n], AssetClassID.SkinnedMeshRenderer);
                var renderer = skinned ?? Component(gameObjects[n], AssetClassID.MeshRenderer);
                if (renderer is null || !renderer["m_Enabled"].AsBool)
                {
                    continue;
                }

                var parts = new List<(string Texture, bool CullBack, bool Emissive)>();
                foreach (var pointer in renderer["m_Materials.Array"].Children)
                {
                    var material = Get(pointer);
                    var main = material["m_SavedProperties.m_TexEnvs.Array"].Children.First(t => t["first"].AsString == "_MainTex")["second.m_Texture"];
                    var texture = Get(main);
                    var name = texture["m_Name"].AsString;
                    if (!textures.ContainsKey(name))
                    {
                        textures[name] = DecodeTexture(bundle, texture, "dvl0x150");
                    }

                    var floats = material["m_SavedProperties.m_Floats.Array"].Children.ToDictionary(f => f["first"].AsString, f => f["second"].AsFloat);
                    parts.Add((name, floats.GetValueOrDefault("_Cull") == 2, floats.GetValueOrDefault("_EmiPower") > 0));
                }

                var meshPointer = skinned is not null ? skinned["m_Mesh"] : Component(gameObjects[n], AssetClassID.MeshFilter)!["m_Mesh"];
                if (meshPointer["m_FileID"].AsInt != 0)
                {
                    // A built-in mesh lives in Unity's own resources, not the bundle; the only one here is the shadow quad.
                    var (cull, emissive) = (parts[0].CullBack, parts[0].Emissive);
                    meshes.Add(new CharacterMesh(n, QuadPositions, [.. QuadPositions.Select(_ => -Vector3.UnitZ)], QuadUvs, [], [], [],
                        [new CharacterPart([0, 3, 1, 3, 0, 2], parts[0].Texture, cull, emissive)], Shadow: true));
                    continue;
                }

                var bones = skinned?["m_Bones.Array"].Children.Select(b => byTransform[b["m_PathID"].AsLong]).ToArray() ?? [];
                meshes.Add(ReadMesh(Get(meshPointer), n, bones, parts));
            }

            var placed = gameObjects.FindIndex(go => Component(go, AssetClassID.Animator) is not null);
            var (curves, bindings, loopTicks) = ReadIdle(Get(Component(gameObjects[placed], AssetClassID.Animator)!["m_Controller"]), Get, nodes, placed);
            var fire = nodes.FindIndex(node => node.Name == "h_fire");
            return new CharacterData([.. nodes], placed, fire, [.. meshes], textures, curves, bindings, loopTicks, ramp);
        }
        finally
        {
            manager.UnloadAll();
        }
    }

    private static CharacterMesh ReadMesh(AssetTypeValueField mesh, int node, int[] bones, List<(string Texture, bool CullBack, bool Emissive)> parts)
    {
        if (mesh["m_StreamData.size"].AsUInt > 0 || mesh["m_CompressedMesh.m_Vertices.m_NumItems"].AsUInt > 0)
        {
            throw new InvalidDataException($"Mesh {mesh["m_Name"].AsString} is streamed or compressed, which the port doesn't read.");
        }

        var vertexData = mesh["m_VertexData"];
        var count = (int)vertexData["m_VertexCount"].AsUInt;
        var channels = vertexData["m_Channels.Array"].Children
            .Select(c => new VertexChannel(c["stream"].AsByte, c["offset"].AsByte, c["format"].AsByte, c["dimension"].AsByte & 0xF))
            .ToArray();
        var data = vertexData["m_DataSize"].AsByteArray;
        var positions = UnityFormats.Read(data, channels, UnityFormats.Position, count)!;
        var normals = UnityFormats.Read(data, channels, UnityFormats.Normal, count)!;
        var uvs = UnityFormats.Read(data, channels, UnityFormats.Uv0, count)!;
        var weights = UnityFormats.Read(data, channels, UnityFormats.BlendWeight, count);
        var indices = UnityFormats.Read(data, channels, UnityFormats.BlendIndices, count);

        var influences = new (int Bone, float Weight)[count][];
        if (indices is not null)
        {
            var per = indices.Length / count;
            for (var v = 0; v < count; v++)
            {
                // A mesh with blend indices but no weights gives each vertex all of its one bone.
                influences[v] = [.. Enumerable.Range(0, per).Select(i => ((int)indices[(v * per) + i], weights?[(v * per) + i] ?? 1f)).Where(x => x.Item2 > 0)];
            }
        }

        var buffer = mesh["m_IndexBuffer.Array"].AsByteArray;
        var wide = mesh["m_IndexFormat"].AsInt == 1;
        var submeshes = mesh["m_SubMeshes.Array"].Children.ToArray();
        var built = submeshes.Select((s, i) => new CharacterPart(
            UnityFormats.Indices(buffer, wide, (int)s["firstByte"].AsUInt, (int)s["indexCount"].AsUInt), parts[i].Texture, parts[i].CullBack, parts[i].Emissive)).ToArray();
        var bindPoses = mesh["m_BindPose.Array"].Children
            .Select(m => UnityFormats.Matrix([.. Enumerable.Range(0, 16).Select(i => m[$"e{i / 4}{i % 4}"].AsFloat)]))
            .ToArray();

        return new CharacterMesh(node,
            [.. Enumerable.Range(0, count).Select(v => new Vector3(positions[3 * v], positions[(3 * v) + 1], positions[(3 * v) + 2]))],
            [.. Enumerable.Range(0, count).Select(v => new Vector3(normals[3 * v], normals[(3 * v) + 1], normals[(3 * v) + 2]))],
            [.. Enumerable.Range(0, count).Select(v => new Vector2(uvs[2 * v], uvs[(2 * v) + 1]))],
            bones, bindPoses, indices is null ? [] : influences, built, Shadow: false);
    }

    /// <summary>The Animator's default state's clip: the controller's first state machine, its default state, its first motion.</summary>
    private static (AnimationCurves Curves, NodeBinding[] Bindings, int LoopTicks) ReadIdle(
        AssetTypeValueField controller, Func<AssetTypeValueField, AssetTypeValueField> get, List<CharacterNode> nodes, int animated)
    {
        var machine = controller["m_Controller.m_StateMachineArray.Array"].Children[0]["data"];
        var state = machine["m_StateConstantArray.Array"].Children[(int)machine["m_DefaultState"].AsUInt]["data"];
        var leaf = state["m_BlendTreeConstantArray.Array"].Children[0]["data.m_NodeArray.Array"].Children[0]["data"];
        var clip = get(controller["m_AnimationClips.Array"].Children[(int)leaf["m_ClipID"].AsUInt]);

        var muscle = clip["m_MuscleClip"];
        var data = muscle["m_Clip.data"];
        var streamed = data["m_StreamedClip.data.Array"].Children.Select(w => w.AsUInt).ToArray();
        var curves = AnimationCurves.Parse(streamed, (int)data["m_StreamedClip.curveCount"].AsUInt, (int)data["m_DenseClip.m_CurveCount"].AsUInt,
            [.. data["m_ConstantClip.data.Array"].Children.Select(f => f.AsFloat)]);

        // Bindings name nodes by the CRC-32 of their path below the animated node, which itself is "".
        var byHash = new Dictionary<uint, int>();
        for (var n = 0; n < nodes.Count; n++)
        {
            if (PathBelow(nodes, n, animated) is { } path)
            {
                byHash[UnityFormats.PathHash(path)] = n;
            }
        }

        var bindings = UnityFormats.Bind(
            clip["m_ClipBindingConstant.genericBindings.Array"].Children.Select(b => (b["path"].AsUInt, (int)b["attribute"].AsUInt, b["typeID"].AsInt)),
            curves.Count);
        var loopTicks = (int)MathF.Round((muscle["m_StopTime"].AsFloat - muscle["m_StartTime"].AsFloat) * 30);
        return (curves, [.. bindings.Select(b => new NodeBinding(byHash[b.Path], b.Attribute, b.FirstCurve))], loopTicks);
    }

    /// <summary>Node <paramref name="n"/>'s path below <paramref name="ancestor"/>, or null when it isn't below it.</summary>
    private static string? PathBelow(List<CharacterNode> nodes, int n, int ancestor)
    {
        var names = new List<string>();
        for (var at = n; at != ancestor; at = nodes[at].Parent)
        {
            if (at < 0)
            {
                return null;
            }

            names.Insert(0, nodes[at].Name);
        }

        return string.Join('/', names);
    }

    private static Vector3 Vector(AssetTypeValueField f) => new(f["x"].AsFloat, f["y"].AsFloat, f["z"].AsFloat);

    private static Quaternion Rotation(AssetTypeValueField f) => new(f["x"].AsFloat, f["y"].AsFloat, f["z"].AsFloat, f["w"].AsFloat);
}
