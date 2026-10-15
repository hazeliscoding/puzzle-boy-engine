namespace PuzzleBoy.Rules.Tests;

public sealed class StageBuilderTests
{
    private const string Valid = """
        1.a.o.G
        ..a.RC.
        """;

    [Fact]
    public void A_new_builder_is_all_wall_with_no_goal_or_character()
    {
        var builder = new StageBuilder();

        Assert.Equal(new Tile(CellKind.Wall), builder[0, 0]);
        Assert.Equal(new Tile(CellKind.Wall), builder[19, 19]);
        Assert.Null(builder.Goal);
        Assert.Empty(builder.Characters);
    }

    [Fact]
    public void A_cell_off_the_grid_is_out_of_range()
    {
        var builder = new StageBuilder();

        Assert.Throws<ArgumentOutOfRangeException>(() => builder[20, 0]);
        Assert.Throws<ArgumentOutOfRangeException>(() => builder[0, -1] = new Tile(CellKind.Floor));
    }

    [Fact]
    public void A_stage_drawn_in_code_builds_and_plays()
    {
        var builder = new StageBuilder { Goal = (3, 0) };
        builder[0, 0] = new Tile(CellKind.Floor);
        builder[1, 0] = new Tile(CellKind.Block, 7);
        builder[2, 0] = new Tile(CellKind.Floor);
        builder[3, 0] = new Tile(CellKind.Floor);
        builder.Characters.Add((0, 0));

        Assert.Empty(builder.Validate());
        var board = new Board(builder.Build());
        Assert.Equal(StepResult.Pushed, board.Step(Direction.PlusX));
        Assert.Equal(0x6007, board[2, 0]);
    }

    [Fact]
    public void A_valid_drawing_has_no_problems_and_builds_the_same_cells()
    {
        var drawn = Ascii.Stage(Valid);

        var built = StageBuilder.From(drawn).Build();

        Assert.Equal(drawn.Goal, built.Goal);
        Assert.Equal(drawn.Characters, built.Characters);
        Assert.Equal(drawn.ToFile(), built.ToFile());
    }

    [Fact]
    public void From_renumbers_skipped_slots_in_order()
    {
        var builder = Ascii.Builder("..3.4G");

        Assert.Equal([(2, 0), (4, 0)], builder.Characters);
        Assert.Equal([(0, 2, 0), (1, 4, 0)], builder.Build().Characters);
    }

    [Fact]
    public void ToFile_writes_the_original_layout_and_Parse_reads_it_back()
    {
        var stage = Ascii.Stage("""
            ..3.4
            .aHoG
            .DSC.
            """);

        var file = stage.ToFile();
        var parsed = Stage.Parse(file);

        Assert.Equal(Stage.FileSize, file.Length);
        Assert.Equal([0, 0, 1, 1], file[4..8]);
        Assert.Equal(stage.Characters, parsed.Characters);
        Assert.Equal(stage.Goal, parsed.Goal);
        for (var y = 0; y < Stage.Height; y++)
        {
            for (var x = 0; x < Stage.Width; x++)
            {
                Assert.Equal(stage[x, y], parsed[x, y]);
            }
        }
    }

    [Fact]
    public void A_missing_goal_is_a_problem()
    {
        var builder = Ascii.Builder(Valid);
        builder.Goal = null;

        Assert.Equal([new StageProblem("The stage has no goal. Put one on a floor tile.")], builder.Validate());
    }

    [Fact]
    public void A_goal_off_the_grid_is_a_problem()
    {
        var builder = Ascii.Builder(Valid);
        builder.Goal = (20, 3);

        Assert.Equal("The goal at (20, 3) is off the 20×20 grid.", Single(builder));
    }

    [Theory]
    [InlineData(2, 0, "(2, 0): The goal is on a block. It needs a floor tile.")]
    [InlineData(4, 0, "(4, 0): The goal is on a hole. It needs a floor tile.")]
    [InlineData(7, 0, "(7, 0): The goal is on a wall. It needs a floor tile.")]
    [InlineData(5, 1, "(5, 1): The goal is on a swivel center. It needs a floor tile.")]
    [InlineData(4, 1, "(4, 1): The goal is on a swivel arm. It needs a floor tile.")]
    public void A_goal_off_the_floor_is_a_problem(int x, int y, string expected)
    {
        var builder = Ascii.Builder(Valid);
        builder.Goal = (x, y);

        Assert.Equal(expected, Single(builder));
    }

    [Fact]
    public void No_character_is_a_problem()
    {
        var builder = Ascii.Builder(Valid);
        builder.Characters.Clear();

        Assert.Equal("The stage has no character. Place one to four.", Single(builder));
    }

    [Fact]
    public void A_fifth_character_is_a_problem()
    {
        var builder = Ascii.Builder("1234..G");
        builder.Characters.Add((4, 0));

        Assert.Equal("The stage has 5 characters. It can have at most four.", Single(builder));
    }

    [Fact]
    public void A_character_off_the_grid_is_a_problem()
    {
        var builder = Ascii.Builder(Valid);
        builder.Characters.Add((-1, 5));

        Assert.Equal("Character 2 at (-1, 5) is off the 20×20 grid.", Single(builder));
    }

    [Theory]
    [InlineData(2, 0, "(2, 0): Character 1 starts on a block. Characters need a floor tile.")]
    [InlineData(4, 0, "(4, 0): Character 1 starts on a hole. Characters need a floor tile.")]
    [InlineData(0, 2, "(0, 2): Character 1 starts on a wall. Characters need a floor tile.")]
    [InlineData(4, 1, "(4, 1): Character 1 starts on a swivel arm. Characters need a floor tile.")]
    [InlineData(6, 0, "(6, 0): Character 1 starts on the goal.")]
    public void A_character_off_the_floor_or_on_the_goal_is_a_problem(int x, int y, string expected)
    {
        var builder = Ascii.Builder(Valid);
        builder.Characters[0] = (x, y);

        Assert.Equal(expected, Single(builder));
    }

    [Fact]
    public void Two_characters_on_one_cell_is_a_problem()
    {
        var builder = Ascii.Builder(Valid);
        builder.Characters.Add((0, 0));

        Assert.Equal("(0, 0): Character 2 starts on the same cell as character 1.", Single(builder));
    }

    [Theory]
    [InlineData(CellKind.Floor, 3, false, "(1, 0): This floor has group 3, but only blocks and swivel parts belong to a group.")]
    [InlineData(CellKind.Wall, 1, false, "(1, 0): This wall has group 1, but only blocks and swivel parts belong to a group.")]
    [InlineData(CellKind.Floor, 0, true, "(1, 0): This floor is marked as over a hole, but only blocks and swivel parts rest on a hole. Use a hole tile instead.")]
    [InlineData(CellKind.Hole, 0, true, "(1, 0): This hole is marked as over a hole, but only blocks and swivel parts rest on a hole. Use a hole tile instead.")]
    [InlineData(CellKind.Block, 0, false, "(1, 0): This block has no group. Give it a group from 1 to 2047.")]
    [InlineData(CellKind.Block, 2048, false, "(1, 0): This block has group 2048. Groups run from 1 to 2047.")]
    [InlineData(CellKind.SwivelArm, -1, false, "(1, 0): This swivel arm has group -1. Groups run from 1 to 2047.")]
    [InlineData((CellKind)42, 0, false, "(1, 0): This cell holds an unknown kind of tile (42).")]
    public void A_tile_with_the_wrong_group_or_hole_flag_is_a_problem(CellKind kind, int group, bool overHole, string expected)
    {
        var builder = Ascii.Builder(Valid);
        builder[1, 0] = new Tile(kind, group, overHole);

        Assert.Equal(expected, Single(builder));
    }

    [Fact]
    public void A_group_that_mixes_blocks_and_swivel_parts_is_a_problem()
    {
        var builder = Ascii.Builder(Valid);
        builder[3, 1] = new Tile(CellKind.Block, 32);

        Assert.Equal("(4, 1): Group 32 mixes blocks and swivel parts: there's a block at (3, 1). Give the swivel its own group.", Single(builder));
    }

    [Fact]
    public void A_block_group_in_pieces_is_a_problem()
    {
        var builder = Ascii.Builder("1a.a.G");

        Assert.Equal("(3, 0): Block group 1 is split: this block isn't connected to (1, 0).", Single(builder));
    }

    [Fact]
    public void A_block_group_that_only_touches_diagonally_is_in_pieces()
    {
        var builder = Ascii.Builder("""
            1a..G
            ..a..
            """);

        Assert.Equal("(2, 1): Block group 1 is split: this block isn't connected to (1, 0).", Single(builder));
    }

    [Fact]
    public void A_swivel_without_a_center_is_a_problem() =>
        Assert.Equal("(1, 0): Swivel group 32 has no center. Its arms need one to turn around.", Single(Ascii.Builder("1R..G")));

    [Fact]
    public void A_swivel_with_two_centers_is_a_problem() =>
        Assert.Equal("(4, 0): Swivel group 32 has a second center; the first is at (2, 0). A swivel has exactly one.", Single(Ascii.Builder("1RC.C.G")));

    [Fact]
    public void A_swivel_without_arms_is_a_problem() =>
        Assert.Equal("(2, 0): This center of swivel group 32 has no arms.", Single(Ascii.Builder("1.C.G")));

    [Fact]
    public void An_arm_out_of_line_with_its_center_is_a_problem()
    {
        var builder = Ascii.Builder("""
            1.CRG
            ...R.
            """);

        Assert.Equal("(3, 1): This arm of swivel group 32 isn't in line with its center at (2, 0).", Single(builder));
    }

    [Theory]
    [InlineData("1R.C.G", "(1, 0): This arm of swivel group 32 isn't joined to its center at (3, 0): (2, 0) is in between.")]
    [InlineData("1RoC.G", "(1, 0): This arm of swivel group 32 isn't joined to its center at (3, 0): (2, 0) is in between.")]
    [InlineData("1RaC.G", "(1, 0): This arm of swivel group 32 isn't joined to its center at (3, 0): (2, 0) is in between.")]
    public void An_arm_with_something_else_between_it_and_its_center_is_a_problem(string drawing, string expected) =>
        Assert.Equal(expected, Single(Ascii.Builder(drawing)));

    [Fact]
    public void A_long_arm_of_its_own_swivel_cells_is_joined() =>
        Assert.Empty(Ascii.Builder("1.RHC.G").Validate());

    [Fact]
    public void Build_throws_with_every_problem()
    {
        var builder = Ascii.Builder("1a.a.G");
        builder.Goal = null;
        builder[2, 0] = new Tile(CellKind.Block);

        var problems = builder.Validate();
        var error = Assert.Throws<StageFormatException>(builder.Build);

        Assert.Equal(
            [
                "The stage has no goal. Put one on a floor tile.",
                "(2, 0): This block has no group. Give it a group from 1 to 2047.",
                "(3, 0): Block group 1 is split: this block isn't connected to (1, 0).",
            ],
            problems.Select(p => p.ToString()));
        Assert.Equal(problems, error.Problems);
        Assert.Equal((2, 0), (error.Problems[1].X, error.Problems[1].Y));
        Assert.StartsWith("The stage has 3 problems:\n", error.Message);
        Assert.IsAssignableFrom<FormatException>(error);
    }

    private static string Single(StageBuilder builder) => Assert.Single(builder.Validate()).ToString();
}
