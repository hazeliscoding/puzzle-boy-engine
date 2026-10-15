namespace PuzzleBoy.Rules.Tests;

public sealed class WalkingTests
{
    [Fact]
    public void A_step_onto_floor_moves_one_cell()
    {
        const string Before = """
            ....
            .1..
            """;
        var board = Ascii.Board(Before);

        Assert.Equal(StepResult.Walked, board.Step(Direction.PlusX));
        Assert.Equal("""
            ....
            ..1.
            """, Ascii.Draw(board, Before));
    }

    [Theory]
    [InlineData(Direction.PlusX)]
    [InlineData(Direction.PlusY)]
    [InlineData(Direction.MinusY)]
    [InlineData(Direction.MinusX)]
    public void Walls_holes_swivel_centers_and_the_grid_edge_refuse_a_step(Direction direction)
    {
        // Right is a wall, down a hole, up a swivel center, and left is off the grid.
        const string Before = """
            C...
            1#..
            o...
            """;
        var board = Ascii.Board(Before);

        Assert.Equal(StepResult.Refused, board.Step(direction));
        Assert.Equal(Before, Ascii.Draw(board, Before));
    }

    [Fact]
    public void Another_character_refuses_a_step()
    {
        const string Before = "12.";
        var board = Ascii.Board(Before);

        Assert.Equal(StepResult.Refused, board.Step(Direction.PlusX));
        Assert.Equal(Before, Ascii.Draw(board, Before));
    }
}
