namespace PuzzleBoy.Rules.Tests;

public sealed class GoalTests
{
    [Fact]
    public void The_last_character_reaching_the_goal_clears_the_stage()
    {
        const string Before = "1G";
        var board = Ascii.Board(Before);

        Assert.Equal(StepResult.Walked, board.Step(Direction.PlusX));
        Assert.True(board.IsCleared);
        Assert.Equal(StepResult.Refused, board.Step(Direction.MinusX));
    }

    [Fact]
    public void Earlier_arrivals_leave_the_board_and_hand_control_to_the_next_character()
    {
        const string Before = "1G2";
        var board = Ascii.Board(Before);

        board.Step(Direction.PlusX);

        Assert.False(board.IsCleared);
        Assert.Equal(1, board.Active.Slot);
        Assert.Equal(".G2", Ascii.Draw(board, Before));

        board.Step(Direction.MinusX);

        Assert.True(board.IsCleared);
    }

    [Fact]
    public void Control_skips_characters_that_already_reached_the_goal()
    {
        const string Before = """
            2G3
            .1.
            """;
        var board = Ascii.Board(Before);

        board.Step(Direction.MinusY);
        Assert.Equal(1, board.Active.Slot);

        board.Step(Direction.PlusX);
        Assert.Equal(2, board.Active.Slot);
        Assert.False(board.IsCleared);

        board.Step(Direction.MinusX);
        Assert.True(board.IsCleared);
    }
}
