namespace PuzzleBoy.Rules.Tests;

public sealed class SwivelTests
{
    [Fact]
    public void Pushing_an_arm_turns_the_swivel_a_quarter_turn_along_the_push()
    {
        const string Before = """
            .....
            .1...
            .RC..
            .....
            .....
            """;
        var board = Ascii.Board(Before);

        Assert.Equal(StepResult.Turned, board.Step(Direction.PlusY));
        Assert.Equal("""
            .....
            .....
            .1C..
            ..R..
            .....
            """, Ascii.Draw(board, Before));
    }

    [Fact]
    public void Pushing_an_arm_from_the_other_side_turns_the_other_way()
    {
        const string Before = """
            .....
            ...1.
            ..CR.
            .....
            .....
            """;
        var board = Ascii.Board(Before);

        Assert.Equal(StepResult.Turned, board.Step(Direction.PlusY));
        Assert.Equal("""
            .....
            .....
            ..C1.
            ..R..
            .....
            """, Ascii.Draw(board, Before));
    }

    [Theory]
    [InlineData(0, -1, Direction.PlusX)]
    [InlineData(0, -1, Direction.MinusX)]
    [InlineData(1, 0, Direction.PlusY)]
    [InlineData(1, 0, Direction.MinusY)]
    [InlineData(0, 1, Direction.PlusX)]
    [InlineData(0, 1, Direction.MinusX)]
    [InlineData(-1, 0, Direction.PlusY)]
    [InlineData(-1, 0, Direction.MinusY)]
    public void The_pushed_arm_always_ends_on_the_side_the_push_points_to(int armX, int armY, Direction push)
    {
        var (dx, dy) = Offset(push);
        var (cx, cy) = (4, 4);
        var drawing = Room(9, 9, (cx, cy, 'C'), (cx + armX, cy + armY, 'R'), (cx + armX - dx, cy + armY - dy, '1'));
        var board = Ascii.Board(drawing);

        Assert.Equal(StepResult.Turned, board.Step(push));
        Assert.Equal(0xA020, board[cx + dx, cy + dy]);
        Assert.Equal((cx + armX, cy + armY), (board.Active.X, board.Active.Y));
    }

    [Fact]
    public void Pushing_an_arm_toward_the_center_is_refused()
    {
        const string Before = """
            ..1..
            ..R..
            ..C..
            """;
        var board = Ascii.Board(Before);

        Assert.Equal(StepResult.Refused, board.Step(Direction.PlusY));
        Assert.Equal(Before, Ascii.Draw(board, Before));
    }

    [Theory]
    [InlineData("""
        .....
        .1...
        .RC..
        .#...
        """)]
    [InlineData("""
        .....
        .1...
        .RC..
        ..#..
        """)]
    [InlineData("""
        .....
        .1...
        .RC..
        ..Ra.
        """)]
    public void A_wall_or_another_group_on_the_swept_or_landing_cells_refuses_the_turn(string before)
    {
        // Cases: a wall on the pushed arm's swept cell, a wall where it lands, and a block on the second arm's swept cell.
        var board = Ascii.Board(before);

        Assert.Equal(StepResult.Refused, board.Step(Direction.PlusY));
        Assert.Equal(before, Ascii.Draw(board, before));
    }

    [Fact]
    public void An_arm_landing_on_the_swivels_own_cell_is_allowed()
    {
        const string Before = """
            .....
            .1...
            .RC..
            ..R..
            .....
            """;
        var board = Ascii.Board(Before);

        Assert.Equal(StepResult.Turned, board.Step(Direction.PlusY));
        Assert.Equal("""
            .....
            .....
            .1CR.
            ..R..
            .....
            """, Ascii.Draw(board, Before));
    }

    [Fact]
    public void Another_swivels_arm_on_a_swept_cell_refuses_the_turn()
    {
        const string Before = """
            .....
            .1...
            .RC..
            ..RS.
            """;
        var board = Ascii.Board(Before);

        Assert.Equal(StepResult.Refused, board.Step(Direction.PlusY));
    }

    [Fact]
    public void Holes_never_block_a_turn_and_an_arm_can_rest_on_a_hole_without_filling_it()
    {
        const string Before = """
            .....
            .1...
            .RC..
            .oo..
            """;
        var board = Ascii.Board(Before);

        Assert.Equal(StepResult.Turned, board.Step(Direction.PlusY));
        Assert.Equal("""
            .....
            .....
            .1C..
            .oH..
            """, Ascii.Draw(board, Before));
    }

    [Fact]
    public void An_arm_resting_on_a_hole_cannot_be_pushed_directly()
    {
        // Stage 36, the sixth stage the original plays, has an arm like this.
        const string Before = """
            .....
            ..R..
            ..C..
            .1H..
            """;
        var board = Ascii.Board(Before);

        Assert.Equal(StepResult.Refused, board.Step(Direction.PlusX));
    }

    [Fact]
    public void Turning_an_arm_off_a_hole_leaves_the_hole_open()
    {
        const string Before = """
            .....
            .1R..
            ..C..
            ..H..
            .....
            """;
        var board = Ascii.Board(Before);

        Assert.Equal(StepResult.Turned, board.Step(Direction.PlusX));
        Assert.Equal("""
            .....
            ..1..
            .RCR.
            ..o..
            .....
            """, Ascii.Draw(board, Before));
    }

    [Fact]
    public void The_character_moves_two_cells_when_the_swivel_is_beside_it()
    {
        const string Before = """
            .....
            .1R..
            .RC..
            .....
            .....
            """;
        var board = Ascii.Board(Before);

        Assert.Equal(StepResult.Turned, board.Step(Direction.PlusY));
        Assert.Equal("""
            .....
            .....
            .RC..
            .1R..
            .....
            """, Ascii.Draw(board, Before));
    }

    [Fact]
    public void A_hole_where_a_two_cell_step_ends_refuses_the_turn()
    {
        const string Before = """
            .....
            .1R..
            .RC..
            .o...
            """;
        var board = Ascii.Board(Before);

        Assert.Equal(StepResult.Refused, board.Step(Direction.PlusY));
    }

    [Fact]
    public void An_arm_can_sweep_over_and_stop_on_the_goal()
    {
        const string Before = """
            .....
            .1...
            .RC..
            ..G..
            """;
        var board = Ascii.Board(Before);

        Assert.Equal(StepResult.Turned, board.Step(Direction.PlusY));
        Assert.Equal(0xA020, board[2, 3]);
        Assert.False(board.IsCleared);
    }

    private static (int X, int Y) Offset(Direction direction) => direction switch
    {
        Direction.PlusY => (0, 1),
        Direction.PlusX => (1, 0),
        Direction.MinusY => (0, -1),
        _ => (-1, 0),
    };

    private static string Room(int width, int height, params (int X, int Y, char C)[] things)
    {
        var rows = Enumerable.Range(0, height).Select(_ => Enumerable.Repeat('.', width).ToArray()).ToArray();
        foreach (var (x, y, c) in things)
        {
            rows[y][x] = c;
        }

        return string.Join('\n', rows.Select(r => new string(r)));
    }
}
