using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PuzzleBoy.Nocturne;

namespace PuzzleBoy.Rendering;

/// <summary>
/// Draws Jack-o'-Lantern the way re/m3-jack.md fits the HD look with built-in effects: each material's texture times a
/// toon ramp from <c>tone2</c>, no outline, a shadow that subtracts from the floor and a generated lantern glow.
/// </summary>
internal sealed class CharacterView : IDisposable
{
    // The ramp is tone2's band 4, Unity row 36 from the bottom. INFERRED from footage, like the light and the 1.1x
    // brightness: DualTextureEffect doubles texture x ramp, so 0.55 gives 1.1.
    private const int RampRow = 36;
    private const float Brightness = 0.55f;

    // The light comes from screen-left, about 24 degrees up and toward the camera, at view yaw 0.
    private static readonly Vector3 Light = Vector3.Normalize(new Vector3(-0.8f, 0.4f, 0.4f));

    // INFERRED: the footage's shadow takes 77-92 levels off the floor; the material's own 110 at 80% fits.
    private const float ShadowStrength = 0.8f;

    // The glow: a camera-facing square 1.5 cells wide, adding up to (120, 84, 40) at its center.
    private const float GlowHalf = 0.75f;

    private static readonly BlendState Subtract = new()
    {
        ColorBlendFunction = BlendFunction.ReverseSubtract,
        ColorSourceBlend = Blend.SourceAlpha,
        ColorDestinationBlend = Blend.One,
        AlphaSourceBlend = Blend.Zero,
        AlphaDestinationBlend = Blend.One,
    };

    private static readonly BlendState Add = new()
    {
        ColorSourceBlend = Blend.One,
        ColorDestinationBlend = Blend.One,
        AlphaSourceBlend = Blend.Zero,
        AlphaDestinationBlend = Blend.One,
    };

    private readonly GraphicsDevice _device;
    private readonly Dictionary<string, Texture2D> _textures = [];
    private readonly Texture2D _ramp;
    private readonly Texture2D _glow;
    private readonly DualTextureEffect _toon;
    private readonly BasicEffect _flat;
    private readonly short[][][] _indices;

    /// <summary>Uploads the model's textures, the ramp strip and the glow.</summary>
    public CharacterView(GraphicsDevice device, CharacterData data)
    {
        _device = device;
        foreach (var (name, texture) in data.Textures)
        {
            _textures[name] = BoardView.Upload(device, texture);
        }

        _ramp = new Texture2D(device, data.Ramp.Width, 1);
        _ramp.SetData(data.Ramp.Levels[0], RampRow * data.Ramp.Width * 4, data.Ramp.Width * 4);
        _glow = new Texture2D(device, 64, 64);
        _glow.SetData(GlowTexels());
        _toon = new DualTextureEffect(device) { DiffuseColor = new Vector3(Brightness), VertexColorEnabled = false };
        _flat = new BasicEffect(device) { TextureEnabled = true, LightingEnabled = false, VertexColorEnabled = false };

        // The Reach profile takes 16-bit indices only; every mesh here is far below 65,536 vertices.
        _indices = [.. data.Meshes.Select(m => m.Parts.Select(p => p.Indices.Select(i => (short)i).ToArray()).ToArray())];
    }

    /// <summary>Draws the posed models, opaque, with the light turned to stay screen-left at view yaw <paramref name="viewYaw"/> degrees.</summary>
    public void DrawModels(IEnumerable<CharacterModel> models, Matrix view, Matrix projection, float viewYaw)
    {
        // UNKNOWN whether the original's light turns with the view; the footage never turns it. This keeps it screen-left.
        var light = Vector3.TransformNormal(Light, Matrix.CreateRotationY(MathHelper.ToRadians(viewYaw)));
        _toon.View = view;
        _toon.Projection = projection;
        _toon.World = Matrix.Identity;
        _device.DepthStencilState = DepthStencilState.Default;
        _device.BlendState = BlendState.Opaque;
        _device.SamplerStates[0] = SamplerState.LinearWrap;
        _device.SamplerStates[1] = SamplerState.LinearClamp;
        foreach (var model in models)
        {
            for (var m = 0; m < model.Data.Meshes.Length; m++)
            {
                var mesh = model.Data.Meshes[m];
                if (mesh.Shadow)
                {
                    continue;
                }

                var (positions, normals) = (new Vector3[mesh.Positions.Length], new Vector3[mesh.Positions.Length]);
                model.Skin(m, positions, normals);
                for (var p = 0; p < mesh.Parts.Length; p++)
                {
                    // The ramp coordinate is half-Lambert: 0 facing the light, 1 facing away. Self-lit parts use 0.
                    var part = mesh.Parts[p];
                    var vertices = new RampVertex[positions.Length];
                    for (var v = 0; v < vertices.Length; v++)
                    {
                        var ramp = part.Emissive ? 0 : (1 - Vector3.Dot(normals[v], light)) / 2;
                        vertices[v] = new RampVertex(positions[v], mesh.Uvs[v], new Vector2(ramp, 0.5f));
                    }

                    _toon.Texture = _textures[part.Texture];
                    _toon.Texture2 = _ramp;
                    _device.RasterizerState = part.CullBack ? RasterizerState.CullCounterClockwise : RasterizerState.CullNone;
                    Submit(_toon, vertices, _indices[m][p]);
                }
            }
        }
    }

    /// <summary>Draws each model's shadow, then its lantern glow, over the opaque scene.</summary>
    public void DrawShadowsAndGlows(IEnumerable<CharacterModel> models, Matrix view, Matrix projection)
    {
        var camera = Matrix.Invert(view);
        _flat.View = view;
        _flat.Projection = projection;
        _flat.World = Matrix.Identity;
        _device.DepthStencilState = DepthStencilState.DepthRead;
        _device.SamplerStates[0] = SamplerState.LinearClamp;
        foreach (var model in models)
        {
            for (var m = 0; m < model.Data.Meshes.Length; m++)
            {
                var mesh = model.Data.Meshes[m];
                if (!mesh.Shadow)
                {
                    continue;
                }

                var (positions, normals) = (new Vector3[mesh.Positions.Length], new Vector3[mesh.Positions.Length]);
                model.Skin(m, positions, normals);
                var vertices = positions.Select((p, v) => new VertexPositionTexture(p, mesh.Uvs[v])).ToArray();
                _flat.Texture = _textures[mesh.Parts[0].Texture];
                _flat.Alpha = ShadowStrength;
                _device.BlendState = Subtract;
                _device.RasterizerState = RasterizerState.CullCounterClockwise;
                Submit(_flat, vertices, _indices[m][0]);
            }

            // INFERRED: the glow is an effect the game attaches to the lantern's hook point; its code is protected.
            var fire = model.World(model.Data.Fire).Translation;
            var (right, up) = (camera.Right * GlowHalf, camera.Up * GlowHalf);
            VertexPositionTexture[] quad =
            [
                new(fire - right - up, new Vector2(0, 1)), new(fire + right - up, new Vector2(1, 1)),
                new(fire - right + up, new Vector2(0, 0)), new(fire + right + up, new Vector2(1, 0)),
            ];
            _flat.Texture = _glow;
            _flat.Alpha = 1;
            _device.BlendState = Add;
            _device.RasterizerState = RasterizerState.CullNone;
            Submit(_flat, quad, [0, 1, 2, 1, 3, 2]);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var texture in _textures.Values)
        {
            texture.Dispose();
        }

        _ramp.Dispose();
        _glow.Dispose();
        _toon.Dispose();
        _flat.Dispose();
    }

    private void Submit<T>(Effect effect, T[] vertices, short[] indices)
        where T : struct, IVertexType
    {
        foreach (var pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0, vertices.Length, indices, 0, indices.Length / 3);
        }
    }

    /// <summary>The glow: (120, 84, 40) at the center, fading with a smoothstep from 6 to 25 of its 30-unit radius.</summary>
    private static Color[] GlowTexels()
    {
        var texels = new Color[64 * 64];
        for (var y = 0; y < 64; y++)
        {
            for (var x = 0; x < 64; x++)
            {
                var r = MathF.Sqrt(((x - 31.5f) * (x - 31.5f)) + ((y - 31.5f) * (y - 31.5f))) / 32 * 30;
                var s = Math.Clamp((r - 6) / 19, 0, 1);
                var fade = 1 - (s * s * (3 - (2 * s)));
                texels[(y * 64) + x] = new Color(new Vector3(120, 84, 40) / 255 * fade);
            }
        }

        return texels;
    }

    /// <summary>A vertex with the material's texture coordinate and the ramp coordinate, for <see cref="DualTextureEffect"/>.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private readonly struct RampVertex(Vector3 position, Vector2 uv, Vector2 ramp) : IVertexType
    {
        private static readonly VertexDeclaration Declaration = new(
            new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
            new VertexElement(12, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
            new VertexElement(20, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 1));

        private readonly Vector3 _position = position;
        private readonly Vector2 _uv = uv;
        private readonly Vector2 _ramp = ramp;

        VertexDeclaration IVertexType.VertexDeclaration => Declaration;
    }
}
