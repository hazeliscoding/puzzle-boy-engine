namespace PuzzleBoy.Rules.Tests;

public sealed class SwitchTests
{
    [Fact]
    public void Switching_hands_control_to_the_next_character_and_wraps_around()
    {
        var board = Ascii.Board("1.2.3");

        Assert.True(board.Switch());
        Assert.Equal(1, board.Active.Slot);
        Assert.True(board.Switch());
        Assert.Equal(2, board.Active.Slot);
        Assert.True(board.Switch());
        Assert.Equal(0, board.Active.Slot);
    }

    [Fact]
    public void Switching_skips_characters_that_already_reached_the_goal()
    {
        var board = Ascii.Board("1G2.3");

        board.Step(Direction.PlusX);
        Assert.Equal(1, board.Active.Slot);

        board.Switch();
        Assert.Equal(2, board.Active.Slot);
        board.Switch();
        Assert.Equal(1, board.Active.Slot);
    }

    [Fact]
    public void A_lone_character_or_a_cleared_board_refuses_to_switch()
    {
        var alone = Ascii.Board("1G2");
        alone.Step(Direction.PlusX);

        Assert.False(alone.Switch());
        Assert.Equal(1, alone.Active.Slot);

        alone.Step(Direction.MinusX);
        Assert.True(alone.IsCleared);
        Assert.False(alone.Switch());
    }

    [Fact]
    public void A_switch_is_not_recorded_and_undo_returns_control_to_the_pusher()
    {
        const string Before = "1a.2";
        var board = Ascii.Board(Before);

        Assert.True(board.Switch());
        Assert.False(board.CanUndo);
        Assert.Null(board.LastMove);

        board.Switch();
        board.Step(Direction.PlusX);
        board.Switch();
        Assert.Equal(1, board.Active.Slot);

        board.Undo();
        Assert.Equal(0, board.Active.Slot);
        Assert.Equal(Before, Ascii.Draw(board, Before));
    }

    [Fact]
    public void A_switch_lets_the_waiting_character_make_the_push_only_it_can_reach()
    {
        // The unused multi-character stages need this: only 2's cell pushes the block into the hole, and 1 can't
        // get there while 2 waits in it.
        const string Before = """
            #2##
            1a..
            #o##
            #G##
            """;
        var board = Ascii.Board(Before);

        board.Switch();
        Assert.Equal(StepResult.Pushed, board.Step(Direction.PlusY));
        board.Step(Direction.PlusY);
        board.Step(Direction.PlusY);
        Assert.Equal(0, board.Active.Slot);

        board.Step(Direction.PlusX);
        board.Step(Direction.PlusY);
        board.Step(Direction.PlusY);
        Assert.True(board.IsCleared);
    }
}
