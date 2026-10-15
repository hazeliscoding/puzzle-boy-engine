using Microsoft.Xna.Framework;
using PuzzleBoy.Nocturne;
using PuzzleBoy.Rules;

namespace PuzzleBoy.Rendering.Tests;

// A handmade two-node model: "root" with "placed" under it, the node the game places, as re/m3-jack.md describes.
public sealed class CharacterModelTests
{
    private static readonly AnimationCurves NoCurves = AnimationCurves.Parse([], 0, 0, []);

    private static CharacterData Model(params CharacterMesh[] meshes) => new(
        [new("root", -1, Vector3.Zero, Quaternion.Identity, Vector3.One), new("placed", 0, Vector3.Zero, Quaternion.Identity, Vector3.One)],
        Placed: 1, Fire: 1, meshes, new Dictionary<string, TextureData>(), NoCurves, [], LoopTicks: 80, Ramp: new TextureData(1, 1, [[0, 0, 0, 0]]));

    [Fact]
    public void A_game_can_draw_the_character_without_a_board() =>
        Assert.True(typeof(CharacterView).IsPublic, "Community Edition's Classic title draws the Jack-o'-Lantern on its own");

    [Theory]
    [InlineData(Direction.PlusY)]
    [InlineData(Direction.PlusX)]
    [InlineData(Direction.MinusY)]
    [InlineData(Direction.MinusX)]
    public void The_model_front_faces_the_direction_of_its_yaw_a_quarter_cell_down(Direction facing)
    {
        // The model's front is -y in its own axes and up is +z, before the original's -90 degree pitch.
        var model = new CharacterModel(Model());
        var (dx, dy) = facing.Offset();

        model.Pose(0, 3, 5, 90 * (int)facing);

        AssertNear(new Vector3(3 + dx, -0.25f, -(5 + dy)), Vector3.Transform(new Vector3(0, -1, 0), model.World(1)));
        AssertNear(new Vector3(3, 0.75f, -5), Vector3.Transform(new Vector3(0, 0, 1), model.World(1)));
    }

    [Fact]
    public void A_vertex_shared_by_two_bones_lands_between_them()
    {
        var mesh = new CharacterMesh(1, [Vector3.Zero], [Vector3.UnitY], [Vector2.Zero], Bones: [0, 1],
            BindPoses: [Matrix.CreateTranslation(-1, 0, 0), Matrix.CreateTranslation(-3, 0, 0)],
            Influences: [[(0, 0.5f), (1, 0.5f)]], Parts: [], Shadow: false);
        var model = new CharacterModel(Model(mesh));
        model.Pose(0, 0, 0, 0);

        var (positions, normals) = (new Vector3[1], new Vector3[1]);
        model.Skin(0, positions, normals);

        // Bone 0 (root) sits at the origin and bone 1 (placed) a quarter cell down; each bind pose shifts the vertex.
        var expected = (Vector3.Transform(new Vector3(-1, 0, 0), model.World(0)) + Vector3.Transform(new Vector3(-3, 0, 0), model.World(1))) / 2;
        AssertNear(expected, positions[0]);
    }

    [Fact]
    public void Skinning_in_the_bind_pose_matches_drawing_the_mesh_rigidly()
    {
        // With bind pose = the bone's inverse world, a skinned vertex lands where the rigid mesh would put it.
        var model = new CharacterModel(Model());
        model.Pose(0, 2, 1, 90);
        var rigid = new CharacterMesh(1, [new Vector3(0.2f, 0.3f, 0.4f)], [Vector3.UnitZ], [Vector2.Zero], [], [], [], [], false);
        var skinned = rigid with { Bones = [1], BindPoses = [Matrix.Identity], Influences = [[(0, 1f)]] };
        var both = new CharacterModel(Model(rigid, skinned));
        both.Pose(0, 2, 1, 90);

        var (a, b, n) = (new Vector3[1], new Vector3[1], new Vector3[1]);
        both.Skin(0, a, n);
        both.Skin(1, b, n);

        AssertNear(a[0], b[0]);
    }

    private static void AssertNear(Vector3 expected, Vector3 actual) =>
        Assert.True(Vector3.Distance(expected, actual) < 1e-4f, $"expected {expected}, got {actual}");
}
