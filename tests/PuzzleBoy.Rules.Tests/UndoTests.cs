namespace PuzzleBoy.Rules.Tests;

public sealed class UndoTests
{
    // A one-arm swivel the character can keep turning: each lap is four turns, each followed by a walk.
    private const string Turnstile = """
        .....
        .1...
        .RC..
        .....
        .....
        """;

    private static readonly Direction[] Lap =
    [
        Direction.PlusY, Direction.PlusY,
        Direction.PlusX, Direction.PlusX,
        Direction.MinusY, Direction.MinusY,
        Direction.MinusX, Direction.MinusX,
    ];

    [Fact]
    public void Walking_records_nothing_to_undo()
    {
        const string Before = "1..";
        var board = Ascii.Board(Before);
        board.Step(Direction.PlusX);

        Assert.False(board.CanUndo);
        Assert.False(board.Undo());
        Assert.Equal(".1.", Ascii.Draw(board, Before));
    }

    [Fact]
    public void Undo_returns_to_just_before_the_last_push_and_drops_the_walking_since()
    {
        const string Before = """
            ....
            1a..
            ....
            """;
        var board = Ascii.Board(Before);
        board.Step(Direction.PlusX);
        board.Step(Direction.MinusY);
        board.Step(Direction.PlusX);

        Assert.True(board.Undo());
        Assert.Equal(Before, Ascii.Draw(board, Before));
        Assert.False(board.CanUndo);
    }

    [Fact]
    public void A_push_and_its_fall_are_one_undo_and_the_hole_reopens()
    {
        const string Before = "1ao.";
        var board = Ascii.Board(Before);
        board.Step(Direction.PlusX);

        Assert.True(board.Undo());
        Assert.Equal(Before, Ascii.Draw(board, Before));
        Assert.False(board.CanUndo);
    }

    [Fact]
    public void Undo_restores_a_swivel_turn()
    {
        var board = Ascii.Board(Turnstile);
        board.Step(Direction.PlusY);

        Assert.True(board.Undo());
        Assert.Equal(Turnstile, Ascii.Draw(board, Turnstile));
    }

    [Fact]
    public void Thirty_actions_can_all_be_undone()
    {
        var board = Ascii.Board(Turnstile);
        Play(board, turns: 30);

        Assert.Equal(30, UndoAll(board));
        Assert.Equal(Turnstile, Ascii.Draw(board, Turnstile));
    }

    [Fact]
    public void The_thirty_first_action_drops_the_oldest_undo()
    {
        var board = Ascii.Board(Turnstile);
        Play(board, turns: 31);

        Assert.Equal(30, UndoAll(board));
        // The oldest kept undo is the one taken just before the second turn: after the first turn and its walk.
        Assert.Equal("""
            .....
            .....
            ..C..
            .1R..
            .....
            """, Ascii.Draw(board, Turnstile));
    }

    [Fact]
    public void A_new_action_after_an_undo_replaces_what_was_undone()
    {
        var board = Ascii.Board(Turnstile);
        Play(board, turns: 2);
        board.Undo();
        board.Step(Direction.PlusX);

        Assert.Equal(2, UndoAll(board));
        Assert.Equal(Turnstile, Ascii.Draw(board, Turnstile));
    }

    [Fact]
    public void Undo_brings_back_a_character_that_reached_the_goal_and_gives_it_control()
    {
        const string Before = """
            G...
            1a..
            .2..
            """;
        var board = Ascii.Board(Before);
        board.Step(Direction.PlusX);
        board.Step(Direction.MinusX);
        board.Step(Direction.MinusY);
        Assert.Equal(1, board.Active.Slot);

        Assert.True(board.Undo());
        Assert.Equal(0, board.Active.Slot);
        Assert.Equal(Before, Ascii.Draw(board, Before));
    }

    [Fact]
    public void Clearing_the_stage_wipes_the_undo_history()
    {
        const string Before = "G1a.";
        var board = Ascii.Board(Before);
        board.Step(Direction.PlusX);
        board.Step(Direction.MinusX);
        board.Step(Direction.MinusX);

        Assert.True(board.IsCleared);
        Assert.False(board.CanUndo);
    }

    [Fact]
    public void Retry_restores_the_stage_and_wipes_the_undo_history()
    {
        var board = Ascii.Board(Turnstile);
        Play(board, turns: 3);

        board.Retry();

        Assert.Equal(Turnstile, Ascii.Draw(board, Turnstile));
        Assert.False(board.CanUndo);
    }

    private static void Play(Board board, int turns)
    {
        for (var i = 0; i < turns; i++)
        {
            Assert.Equal(StepResult.Turned, board.Step(Lap[i * 2 % Lap.Length]));
            Assert.Equal(StepResult.Walked, board.Step(Lap[(i * 2 % Lap.Length) + 1]));
        }
    }

    private static int UndoAll(Board board)
    {
        var count = 0;
        while (board.Undo())
        {
            count++;
        }

        return count;
    }
}
