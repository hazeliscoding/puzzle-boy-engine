namespace PuzzleBoy.Rules.Tests;

public sealed class LastMoveTests
{
    [Fact]
    public void A_walk_reports_the_mover_and_no_group()
    {
        var board = Ascii.Board("1..");

        board.Step(Direction.PlusX);

        var move = board.LastMove!;
        Assert.Equal((StepResult.Walked, 0, Direction.PlusX, 1, 0), (move.Result, move.Mover, move.Direction, move.Cells, move.Group));
        Assert.Empty(move.Landings);
    }

    [Fact]
    public void A_push_reports_where_the_block_landed()
    {
        var board = Ascii.Board("""
            1a.
            .a.
            """);

        board.Step(Direction.PlusX);

        var move = board.LastMove!;
        Assert.Equal((StepResult.Pushed, 1, false), (move.Result, move.Group, move.Fell));
        Assert.Equal([(2, 0), (2, 1)], move.Landings);
    }

    [Fact]
    public void A_push_into_holes_reports_the_fall()
    {
        var board = Ascii.Board("1ao..");

        board.Step(Direction.PlusX);

        Assert.True(board.LastMove!.Fell);
        Assert.Equal([(2, 0)], board.LastMove.Landings);
    }

    [Fact]
    public void A_turn_reports_the_center_and_the_sense_with_y_up()
    {
        // In the drawing y runs down, so this clockwise turn (x right, y up) looks counterclockwise here.
        var board = Ascii.Board("""
            .....
            .1...
            .RC..
            .....
            """);

        board.Step(Direction.PlusY);

        var move = board.LastMove!;
        Assert.Equal((StepResult.Turned, (2, 2), -1, 1), (move.Result, move.Center, move.Turn, move.Cells));
        Assert.Contains((2, 3), move.Landings);
    }

    [Fact]
    public void A_refused_step_an_undo_and_a_retry_clear_it()
    {
        var board = Ascii.Board("1a.#");

        board.Step(Direction.PlusX);
        Assert.NotNull(board.LastMove);
        board.Step(Direction.PlusX);
        Assert.Null(board.LastMove);

        board.Step(Direction.MinusX);
        board.Undo();
        Assert.Null(board.LastMove);

        board.Step(Direction.PlusX);
        Assert.NotNull(board.LastMove);
        board.Retry();
        Assert.Null(board.LastMove);
    }
}
