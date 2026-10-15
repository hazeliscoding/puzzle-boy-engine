using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PuzzleBoy.Nocturne;
using PuzzleBoy.Rules;

namespace PuzzleBoy.Rendering;

/// <summary>
/// Draws a board the way the original's pbDraw builds it (puzzle-boy's ROADMAP.md, "M3 findings": board): unlit and textured from
/// the <c>sample1</c> atlas, glass walls, white outlines, a translucent goal and the <c>BG_pzl</c> backdrop.
/// Distances are in cells; the original's 40 units are 1 here.
/// </summary>
public sealed class BoardView : IDisposable
{
    // Atlas tiles as the original's Unity UV rectangles (u0, u1, v0, v1). The atlas keeps Unity's row order on the
    // GPU, so the same numbers sample the same texels.
    private static readonly Vector4 BlockTile = new(0, 0.25f, 0, 0.5f);
    private static readonly Vector4 FloorTile = new(0, 0.25f, 0.5f, 1);
    private static readonly Vector4 WallTile = new(0.75f, 1, 0, 1);
    private static readonly Vector4 SwivelTile = new(0.25f, 0.5f, 0.25f, 0.5f);
    private static readonly Vector4 DomeTile = new(0.5f, 0.75f, 0.5f, 1);

    // Glass panels are 39 units wide and 50 tall; outlines are lines 1.2 units wide.
    private const float PanelHalf = 19.5f / 40;
    private const float PanelHeight = 50f / 40;
    private const float LineWidth = 1.2f / 40;

    private static readonly Color Glass = new(1f, 1f, 1f, 0.2f);
    private static readonly Color WallLine = new(128, 128, 128);

    // The original writes (0, 255, 200, 0.3) as float colors; see the goal's blend in Draw.
    private static readonly Color GoalColor = new(0f, 1f, 1f, 0.3f);

    private readonly GraphicsDevice _device;
    private readonly Texture2D _atlas;
    private readonly Texture2D _background;
    private readonly Texture2D _white;
    private readonly BasicEffect _effect;
    private readonly SpriteBatch _sprites;
    private readonly CharacterView _characters;
    private readonly List<VertexPositionColorTexture> _tiles = [];
    private readonly List<VertexPositionColorTexture> _solid = [];
    private readonly List<VertexPositionColorTexture> _glass = [];
    private readonly List<VertexPositionColorTexture> _clear = [];
    private Vector3 _eye;

    /// <summary>Uploads the atlas, the backdrop and the character's textures.</summary>
    public BoardView(GraphicsDevice device, TextureData atlas, TextureData background, CharacterData character)
    {
        _device = device;
        _atlas = Upload(device, atlas);
        _background = Upload(device, background);
        _white = new Texture2D(device, 1, 1);
        _white.SetData([Color.White]);
        _effect = new BasicEffect(device) { TextureEnabled = true, VertexColorEnabled = true, LightingEnabled = false };
        _sprites = new SpriteBatch(device);
        _characters = new CharacterView(device, character);
    }

    /// <summary>Draws <paramref name="board"/> with <paramref name="animation"/>'s step in progress.</summary>
    /// <param name="board">The board to draw.</param>
    /// <param name="animation">The step being played, if any.</param>
    /// <param name="view">The camera's view matrix.</param>
    /// <param name="projection">The camera's projection matrix.</param>
    /// <param name="ticks">Ticks since the game started, for the goal's spin of 1° a tick.</param>
    /// <param name="characters">The characters to draw, already posed.</param>
    /// <param name="viewYaw">The camera's yaw in degrees (<see cref="ViewCamera.Yaw"/>), which the character's light follows.</param>
    public void Draw(Board board, MoveAnimation? animation, Matrix view, Matrix projection, int ticks, IReadOnlyList<CharacterModel> characters, float viewYaw)
    {
        _eye = Matrix.Invert(view).Translation;
        _tiles.Clear();
        _solid.Clear();
        _glass.Clear();
        _clear.Clear();

        var moving = Moving(animation);
        for (var y = 0; y < Stage.Height; y++)
        {
            for (var x = 0; x < Stage.Width; x++)
            {
                AddCell(board, x, y, moving, animation);
            }
        }

        AddMovingParts(board, animation, moving);

        // The goal is a 20-unit cube centered 20 units up, spinning about its own axis.
        // Unity's Rotate((0, 1, 0)) turns clockwise seen from above; the port's flipped z needs the opposite sign.
        var spin = Matrix.CreateRotationY(MathHelper.ToRadians(-ticks));
        var (gx, gy) = board.Goal;
        var goal = Matrix.CreateTranslation(-gx, -0.5f, gy) * spin * Matrix.CreateTranslation(gx, 0.5f, -gy);
        AddBox(_clear, gx, gy, 0.25f, 0.75f, 0.25f, GoalColor, goal, Tiles(Vector4.Zero));

        DrawBackdrop();
        _effect.View = view;
        _effect.Projection = projection;
        _device.RasterizerState = RasterizerState.CullNone;
        _device.SamplerStates[0] = SamplerState.LinearWrap;
        _device.DepthStencilState = DepthStencilState.Default;
        _device.BlendState = BlendState.Opaque;
        Submit(_tiles, _atlas);
        Submit(_solid, _white);
        _characters.DrawModels(characters, view, projection, viewYaw);
        _characters.DrawShadowsAndGlows(characters, view, projection);

        // Glass and the goal blend over everything opaque and don't hide each other.
        _device.DepthStencilState = DepthStencilState.DepthRead;
        _device.BlendState = BlendState.NonPremultiplied;
        _device.RasterizerState = RasterizerState.CullNone;
        _device.SamplerStates[0] = SamplerState.LinearWrap;
        Submit(_glass, _atlas);

        // The original's goal color is (0, 255, 200) at 30% opacity: blending 0.3 x 255 saturates, leaving cyan
        // plus 70% of what's behind. Adding the color unscaled does the same.
        _device.BlendState = BlendState.AlphaBlend;
        Submit(_clear, _white);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _atlas.Dispose();
        _background.Dispose();
        _white.Dispose();
        _effect.Dispose();
        _sprites.Dispose();
        _characters.Dispose();
    }

    /// <summary>The cells of the group the current step moves, drawn on their own while it plays.</summary>
    private static HashSet<(int X, int Y)> Moving(MoveAnimation? animation) =>
        animation is { } a && a.Move.Result != StepResult.Walked && (!a.Move.Fell || a.Falling) ? [.. a.Move.Landings] : [];

    private void AddCell(Board board, int x, int y, HashSet<(int X, int Y)> moving, MoveAnimation? animation)
    {
        var cell = board[x, y];
        var type = cell >> 13;

        // Every cell but a hole or a plain wall stands on a floor cube from -1 to 0. A fallen block's holes
        // stay open until it has sunk.
        var opening = moving.Contains((x, y)) && animation!.Move.Fell;
        if ((cell & Cell.HoleFlag) == 0 && type != 1 && !opening)
        {
            AddBox(_tiles, x, y, -1, 0, 0.5f, Color.White, Matrix.Identity, Tiles(FloorTile));
        }

        if (type == 1)
        {
            AddWall(board, x, y);
        }
        else if (type is >= 2 and <= 5 && !moving.Contains((x, y)))
        {
            AddPart(board, x, y, Cell.KindOf(cell), Cell.GroupOf(cell), Matrix.Identity, outlined: true);
        }
    }

    /// <summary>A wall's glass faces toward each open neighbour: a floor-height band and a panel above it, outlined in grey.</summary>
    private void AddWall(Board board, int x, int y)
    {
        foreach (var (dx, dy) in Sides)
        {
            var (nx, ny) = (x + dx, y + dy);
            if (nx is < 0 or >= Stage.Width || ny is < 0 or >= Stage.Height || board[nx, ny] >> 13 == 1)
            {
                continue;
            }

            var normal = new Vector3(dx, 0, -dy);
            var right = Vector3.Cross(-normal, Vector3.Up);
            var center = new Vector3(x, 0, -y);
            AddFace(_glass, center + (normal * 0.5f), right * 0.5f, -1, 0, Glass, FloorTile);
            var panel = center + (normal * PanelHalf);
            AddFace(_glass, panel, right * PanelHalf, 0, PanelHeight, Glass, WallTile);

            var (a, b) = (panel - (right * PanelHalf), panel + (right * PanelHalf));
            var up = Vector3.Up * PanelHeight;
            AddLine(a, b, WallLine);
            AddLine(b, b + up, WallLine);
            AddLine(b + up, a + up, WallLine);
            AddLine(a + up, a, WallLine);
        }
    }

    /// <summary>The pushed or turned group, slid, turned or sunk back by the part of the move still to come.</summary>
    private void AddMovingParts(Board board, MoveAnimation? animation, HashSet<(int X, int Y)> moving)
    {
        if (animation is null || moving.Count == 0)
        {
            return;
        }

        var move = animation.Move;
        var left = 1 - animation.Slide;
        Matrix transform;
        if (move.Result == StepResult.Pushed)
        {
            var (dx, dy) = move.Direction.Offset();
            transform = Matrix.CreateTranslation(-dx * left, -animation.Sink, dy * left);
        }
        else
        {
            // Turn back about the center, counterclockwise for a clockwise turn (x right, y up).
            var (cx, cy) = move.Center;
            var angle = -move.Turn * MathHelper.PiOver2 * left;
            transform = Matrix.CreateTranslation(-cx, 0, cy) * Matrix.CreateRotationY(angle) * Matrix.CreateTranslation(cx, 0, -cy);
        }

        foreach (var (x, y) in move.Landings)
        {
            // After a fall the landings are floor, so the sinking block is drawn from the move.
            var kind = move.Fell ? CellKind.Block : Cell.KindOf(board[x, y]);
            AddPart(board, x, y, kind, move.Group, transform, outlined: !move.Fell);
        }
    }

    /// <summary>A block or swivel cube from 0 to 1, with white outlines along its top edges where its group ends.</summary>
    private void AddPart(Board board, int x, int y, CellKind kind, int group, Matrix transform, bool outlined)
    {
        var tiles = kind switch
        {
            CellKind.Block => Tiles(BlockTile),
            CellKind.SwivelCenter => Tiles(SwivelTile, top: DomeTile),
            _ => Tiles(SwivelTile),
        };
        AddBox(_tiles, x, y, 0, 1, 0.5f, Color.White, transform, tiles);
        if (!outlined)
        {
            return;
        }

        // The original's outline sits on the top edge on each side whose neighbour isn't the same group.
        foreach (var (dx, dy) in Sides)
        {
            var (nx, ny) = (x + dx, y + dy);
            var inGroup = nx is >= 0 and < Stage.Width && ny is >= 0 and < Stage.Height
                && board[nx, ny] >> 13 is >= 2 and <= 5 && Cell.GroupOf(board[nx, ny]) == group;
            if (!inGroup)
            {
                var edge = new Vector3(x + (dx * 0.5f), 1, -y - (dy * 0.5f));
                var along = new Vector3(dy * 0.5f, 0, dx * 0.5f);
                AddLine(Vector3.Transform(edge - along, transform), Vector3.Transform(edge + along, transform), Color.White);
            }
        }
    }

    /// <summary>A box over cell (<paramref name="x"/>, <paramref name="y"/>) from height <paramref name="bottom"/> to <paramref name="top"/>.</summary>
    private static void AddBox(List<VertexPositionColorTexture> into, float x, float y, float bottom, float top, float half, Color color, Matrix transform, Vector4[] tiles)
    {
        var center = new Vector3(x, 0, -y);
        var start = into.Count;
        for (var i = 0; i < 4; i++)
        {
            var normal = new Vector3(Sides[i].X, 0, -Sides[i].Y);
            AddFace(into, center + (normal * half), Vector3.Cross(-normal, Vector3.Up) * half, bottom, top, color, tiles[i]);
        }

        // Top and bottom: the top's texture runs up toward grid +y, away from the default camera.
        var (east, north) = (Vector3.UnitX * half, -Vector3.UnitZ * half);
        AddQuad(into, center + (Vector3.Up * top) - east - north, east * 2, north * 2, color, tiles[4]);
        AddQuad(into, center + (Vector3.Up * bottom) - east + north, east * 2, -north * 2, color, tiles[5]);
        for (var i = start; i < into.Count; i++)
        {
            var v = into[i];
            into[i] = v with { Position = Vector3.Transform(v.Position, transform) };
        }
    }

    /// <summary>A vertical face centered on <paramref name="center"/>, spanning ±<paramref name="right"/> and <paramref name="bottom"/> to <paramref name="top"/>.</summary>
    private static void AddFace(List<VertexPositionColorTexture> into, Vector3 center, Vector3 right, float bottom, float top, Color color, Vector4 tile)
    {
        var corner = center - right + (Vector3.Up * bottom);
        AddQuad(into, corner, right * 2, Vector3.Up * (top - bottom), color, tile);
    }

    /// <summary>A quad from <paramref name="corner"/> along <paramref name="u"/> and <paramref name="v"/>, mapped to <paramref name="tile"/> with v up the face.</summary>
    private static void AddQuad(List<VertexPositionColorTexture> into, Vector3 corner, Vector3 u, Vector3 v, Color color, Vector4 tile)
    {
        var a = new VertexPositionColorTexture(corner, color, new Vector2(tile.X, tile.Z));
        var b = new VertexPositionColorTexture(corner + u, color, new Vector2(tile.Y, tile.Z));
        var c = new VertexPositionColorTexture(corner + v, color, new Vector2(tile.X, tile.W));
        var d = new VertexPositionColorTexture(corner + u + v, color, new Vector2(tile.Y, tile.W));
        into.AddRange([a, b, c, b, d, c]);
    }

    /// <summary>A line drawn as a thin quad turned to face the camera, like Unity's LineRenderer.</summary>
    private void AddLine(Vector3 a, Vector3 b, Color color)
    {
        var side = Vector3.Cross(b - a, _eye - ((a + b) / 2));
        if (side.LengthSquared() < 1e-12f)
        {
            return;
        }

        side = Vector3.Normalize(side) * (LineWidth / 2);
        AddQuad(_solid, a - side, b - a, side * 2, color, Vector4.Zero);
    }

    private void DrawBackdrop()
    {
        // The original's backdrop is a plane the camera code keeps facing the view, so it reads as a still image.
        // MEASURED by aligning BG_pzl with HD footage (correlation 0.95): it shows 1.55 times larger than fitting
        // a 16:9 screen, with the screen's center over image pixel (960, 372), counted from the top.
        var viewport = _device.Viewport;
        var scale = 1.55f * viewport.Height / _background.Height;
        var origin = new Vector2((viewport.Width / 2f) - (960 * scale), (viewport.Height / 2f) - (372 * scale));
        _sprites.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
        _sprites.Draw(_background, origin, null, Color.White, 0, Vector2.Zero, scale, SpriteEffects.FlipVertically, 0);
        _sprites.End();
    }

    private void Submit(List<VertexPositionColorTexture> vertices, Texture2D texture)
    {
        if (vertices.Count == 0)
        {
            return;
        }

        _effect.Texture = texture;
        _effect.World = Matrix.Identity;
        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _device.DrawUserPrimitives(PrimitiveType.TriangleList, vertices.ToArray(), 0, vertices.Count / 3);
        }
    }

    /// <summary>Uploads every mip level, keeping Unity's row order so Unity's UVs sample the same texels.</summary>
    public static Texture2D Upload(GraphicsDevice device, TextureData data)
    {
        var full = (int)Math.Log2(Math.Max(data.Width, data.Height)) + 1;
        var texture = new Texture2D(device, data.Width, data.Height, data.Levels.Count == full, SurfaceFormat.Color);
        for (var level = 0; level < (data.Levels.Count == full ? full : 1); level++)
        {
            texture.SetData(level, null, data.Levels[level], 0, data.Levels[level].Length);
        }

        return texture;
    }

    /// <summary>One tile per face: the four sides in <see cref="Sides"/> order, then the top and the bottom.</summary>
    private static Vector4[] Tiles(Vector4 tile, Vector4? top = null) => [tile, tile, tile, tile, top ?? tile, tile];

    // +y, +x, -y, -x: the original's direction order.
    private static readonly (int X, int Y)[] Sides = [(0, 1), (1, 0), (0, -1), (-1, 0)];

}
