namespace PuzzleBoy.Rules.Tests;

public sealed class PushingTests
{
    [Fact]
    public void A_push_moves_the_block_one_cell_and_the_character_follows()
    {
        const string Before = "1a.";
        var board = Ascii.Board(Before);

        Assert.Equal(StepResult.Pushed, board.Step(Direction.PlusX));
        Assert.Equal(".1a", Ascii.Draw(board, Before));
    }

    [Theory]
    [InlineData("1a#")]
    [InlineData("1ab.")]
    [InlineData("1aR.")]
    [InlineData("1a2.")]
    public void A_wall_another_block_a_swivel_or_a_character_refuses_a_push(string before)
    {
        var board = Ascii.Board(before);

        Assert.Equal(StepResult.Refused, board.Step(Direction.PlusX));
        Assert.Equal(before, Ascii.Draw(board, before));
    }

    [Fact]
    public void One_blocked_cell_refuses_the_whole_push()
    {
        const string Before = """
            1a..
            .aa#
            """;
        var board = Ascii.Board(Before);

        Assert.Equal(StepResult.Refused, board.Step(Direction.PlusX));
        Assert.Equal(Before, Ascii.Draw(board, Before));
    }

    [Fact]
    public void A_shaped_block_moves_into_its_own_cells()
    {
        const string Before = """
            1a..
            .aa.
            """;
        var board = Ascii.Board(Before);

        Assert.Equal(StepResult.Pushed, board.Step(Direction.PlusX));
        Assert.Equal("""
            .1a.
            ..aa
            """, Ascii.Draw(board, Before));
    }

    [Fact]
    public void A_block_can_be_pushed_onto_the_goal_without_clearing_the_stage()
    {
        const string Before = "1aG";
        var board = Ascii.Board(Before);

        Assert.Equal(StepResult.Pushed, board.Step(Direction.PlusX));
        Assert.Equal(".1a", Ascii.Draw(board, Before));
        Assert.False(board.IsCleared);
    }

    [Fact]
    public void Another_character_blocks_only_its_own_cell_not_the_mirrored_one()
    {
        // The original swaps x and y in this check, so a character at (0, 2) would block (2, 0).
        // No stage it plays has two characters; the port checks the real cell.
        const string Before = """
            1a.
            ...
            2..
            """;
        var board = Ascii.Board(Before);

        Assert.Equal(StepResult.Pushed, board.Step(Direction.PlusX));
    }

    [Fact]
    public void A_block_falls_into_holes_it_covers_completely_and_fills_them()
    {
        const string Before = "1ao..";
        var board = Ascii.Board(Before);

        Assert.Equal(StepResult.Pushed, board.Step(Direction.PlusX));
        Assert.Equal(".1...", Ascii.Draw(board, Before));
        Assert.Equal(StepResult.Walked, board.Step(Direction.PlusX));
        Assert.Equal(StepResult.Walked, board.Step(Direction.PlusX));
    }

    [Fact]
    public void A_fall_fills_only_the_covered_holes()
    {
        const string Before = """
            1ao
            .ao
            ..o
            """;
        var board = Ascii.Board(Before);

        Assert.Equal(StepResult.Pushed, board.Step(Direction.PlusX));
        Assert.Equal("""
            .1.
            ...
            ..o
            """, Ascii.Draw(board, Before));
    }

    [Fact]
    public void A_block_partly_over_holes_stays_up_and_its_holes_stay_open()
    {
        const string Before = """
            1a..
            .ao.
            """;
        var board = Ascii.Board(Before);

        Assert.Equal(StepResult.Pushed, board.Step(Direction.PlusX));
        Assert.Equal("""
            .1a.
            ..a.
            """, Ascii.Draw(board, Before));
        Assert.Equal(Cell.HoleFlag, board[2, 1] & Cell.HoleFlag);
    }

    [Fact]
    public void A_block_cell_resting_on_a_hole_cannot_be_pushed_but_its_other_cells_can()
    {
        const string Before = """
            1a..
            .ao.
            """;
        var board = Ascii.Board(Before);
        board.Step(Direction.PlusX);
        board.Step(Direction.PlusY);

        Assert.Equal(StepResult.Refused, board.Step(Direction.PlusX));

        board.Step(Direction.MinusY);
        Assert.Equal(StepResult.Pushed, board.Step(Direction.PlusX));
        Assert.Equal("""
            ..1a
            ..oa
            """, Ascii.Draw(board, Before));
    }
}
