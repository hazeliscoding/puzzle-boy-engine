using Microsoft.Xna.Framework;
using PuzzleBoy.Nocturne;

namespace PuzzleBoy.Rendering;

/// <summary>
/// Poses and skins Jack-o'-Lantern each tick, as re/m3-jack.md describes: the idle clip on the bones, the original's
/// placement and facing, then CPU skinning into the port's space (cells, grid +y toward -z).
/// </summary>
/// <param name="data">The model read from the player's install.</param>
public sealed class CharacterModel(CharacterData data)
{
    // The original drops its model a quarter cell (10 units) below the character's position.
    private const float Height = -0.25f;

    private readonly Matrix[] _world = new Matrix[data.Nodes.Length];
    private readonly Vector3[] _positions = new Vector3[data.Nodes.Length];
    private readonly Quaternion[] _rotations = new Quaternion[data.Nodes.Length];
    private readonly Vector3[] _scales = new Vector3[data.Nodes.Length];

    /// <summary>The model.</summary>
    public CharacterData Data => data;

    /// <summary>Node <paramref name="node"/>'s world matrix from the last <see cref="Pose"/>, in the port's space.</summary>
    public Matrix World(int node) => _world[node];

    /// <summary>
    /// Poses the idle clip at <paramref name="tick"/> (it loops) and places the model on grid cell (<paramref name="x"/>,
    /// <paramref name="y"/>), which may be fractional mid-step, turned <paramref name="yaw"/> degrees: 0 faces grid +y,
    /// 90 grid +x.
    /// </summary>
    public void Pose(int tick, float x, float y, float yaw)
    {
        for (var n = 0; n < data.Nodes.Length; n++)
        {
            (_positions[n], _rotations[n], _scales[n]) = (data.Nodes[n].Position, data.Nodes[n].Rotation, data.Nodes[n].Scale);
        }

        // An integer phase keeps the loop seam exact.
        var time = (tick % data.LoopTicks) / 30f;
        foreach (var (node, attribute, first) in data.Bindings)
        {
            var c = data.Curves;
            switch (attribute)
            {
                case 1:
                    _positions[node] = new Vector3(c.Value(first, time), c.Value(first + 1, time), c.Value(first + 2, time));
                    break;
                case 2:
                    _rotations[node] = Quaternion.Normalize(new Quaternion(c.Value(first, time), c.Value(first + 1, time), c.Value(first + 2, time), c.Value(first + 3, time)));
                    break;
                default:
                    _scales[node] = new Vector3(c.Value(first, time), c.Value(first + 1, time), c.Value(first + 2, time));
                    break;
            }
        }

        // Unity's Euler(-90, yaw, 0) applies x, then y, at the original's scale of 40, which is 1 cell per model unit
        // here. The final mirror of z turns Unity's left-handed world into the port's.
        var placed = Matrix.CreateRotationX(MathHelper.ToRadians(-90)) * Matrix.CreateRotationY(MathHelper.ToRadians(yaw))
            * Matrix.CreateTranslation(x, Height, y) * Matrix.CreateScale(1, 1, -1);
        for (var n = 0; n < data.Nodes.Length; n++)
        {
            var local = Matrix.CreateScale(_scales[n]) * Matrix.CreateFromQuaternion(_rotations[n]) * Matrix.CreateTranslation(_positions[n]);
            var parent = data.Nodes[n].Parent;
            _world[n] = n == data.Placed ? placed : parent < 0 ? local : local * _world[parent];
        }
    }

    /// <summary>Mesh <paramref name="mesh"/>'s vertices and normals from the last <see cref="Pose"/>, into arrays its size.</summary>
    public void Skin(int mesh, Vector3[] positions, Vector3[] normals)
    {
        var m = data.Meshes[mesh];
        if (m.Bones.Length == 0)
        {
            var world = _world[m.Node];
            for (var v = 0; v < m.Positions.Length; v++)
            {
                positions[v] = Vector3.Transform(m.Positions[v], world);
                normals[v] = Vector3.Normalize(Vector3.TransformNormal(m.Normals[v], world));
            }

            return;
        }

        // Each bone's skin matrix takes a vertex from the bind pose to the bone's posed world.
        var skin = new Matrix[m.Bones.Length];
        for (var b = 0; b < skin.Length; b++)
        {
            skin[b] = m.BindPoses[b] * _world[m.Bones[b]];
        }

        for (var v = 0; v < m.Positions.Length; v++)
        {
            var (position, normal) = (Vector3.Zero, Vector3.Zero);
            foreach (var (bone, weight) in m.Influences[v])
            {
                position += Vector3.Transform(m.Positions[v], skin[bone]) * weight;
                normal += Vector3.TransformNormal(m.Normals[v], skin[bone]) * weight;
            }

            positions[v] = position;
            normals[v] = Vector3.Normalize(normal);
        }
    }
}
