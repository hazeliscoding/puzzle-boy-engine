using System.Buffers.Binary;
using System.Text;

namespace PuzzleBoy.Rules.Tests;

// What both games rely on. Changing any of it is a public API change: see AGENTS.md.
public sealed class SharedBehaviorTests
{
    private const string Drawing = """
        1a.o..G
        .......
        .2.R...
        ...C...
        .......
        """;

    // Walks, pushes, a fall, an undo, a turn, switches, a refused step, a goal arrival and a retry.
    private static readonly string[] Inputs =
    [
        "Switch", "+X", "+X", "Switch", "+X", "+X", "Undo", "+X", "-Y", "+X", "+X", "+X", "+X",
        "+X", "-Y", "-X", "+Y", "Undo", "Switch", "+X", "+Y", "+Y", "-X", "Retry", "+X", "+Y", "Switch", "-X",
    ];

    [Fact]
    public void A_stage_built_in_code_parsed_from_bytes_or_read_from_json_plays_the_same()
    {
        var builder = new StageBuilder { Goal = (6, 0) };
        for (var y = 0; y < 5; y++)
        {
            for (var x = 0; x < 7; x++)
            {
                builder[x, y] = new Tile(CellKind.Floor);
            }
        }

        builder[1, 0] = new Tile(CellKind.Block, 1);
        builder[3, 0] = new Tile(CellKind.Hole);
        builder[3, 2] = new Tile(CellKind.SwivelArm, 32);
        builder[3, 3] = new Tile(CellKind.SwivelCenter, 32);
        builder.Characters.AddRange([(0, 0), (1, 2)]);
        var built = builder.Build();

        Board[] boards =
        [
            new(built),
            new(Ascii.Stage(Drawing)),
            new(Stage.Parse(built.ToFile())),
            new(StageDocument.Parse(new StageDocument(built, null).ToJson()).Stage),
        ];

        var results = new List<StepResult>();
        var fell = false;
        foreach (var input in Inputs)
        {
            foreach (var board in boards)
            {
                var result = Apply(board, input);
                if (board == boards[0] && result is { } step)
                {
                    results.Add(step);
                    fell |= board.LastMove?.Fell == true;
                }
            }

            var expected = Snapshot(boards[0]);
            Assert.All(boards, board => Assert.Equal(expected, Snapshot(board)));
        }

        Assert.Equal([StepResult.Walked, StepResult.Pushed, StepResult.Turned, StepResult.Refused], results.Distinct().Order());
        Assert.True(fell);
    }

    [Fact]
    public void Board_keeps_its_public_switch()
    {
        var method = typeof(Board).GetMethod(nameof(Board.Switch), Type.EmptyTypes);

        Assert.NotNull(method);
        Assert.Equal(typeof(bool), method.ReturnType);
        Assert.True(Ascii.Board("1.2G").Switch());
    }

    [Fact]
    public void Parse_stays_lenient_about_stages_the_builder_rejects()
    {
        // The goal on a wall, a group that mixes blocks and a swivel arm, a block in pieces and a swivel with no center.
        var file = Ascii.Stage("1a.a.b.b").ToFile();
        WriteCell(file, 9, 0, 0xA001);
        WriteCell(file, 11, 0, 0xA005);

        var parsed = Stage.Parse(file);

        Assert.Equal(CellKind.Wall, Cell.KindOf(parsed[parsed.Goal.X, parsed.Goal.Y]));
        Assert.Equal(0xA001, parsed[9, 0]);
        Assert.Equal(0xA005, parsed[11, 0]);
        Assert.Equal(
            [
                "(19, 19): The goal is on a wall. It needs a floor tile.",
                "(9, 0): Group 1 mixes blocks and swivel parts: there's a block at (1, 0). Give the swivel its own group.",
                "(7, 0): Block group 2 is split: this block isn't connected to (5, 0).",
                "(11, 0): Swivel group 5 has no center. Its arms need one to turn around.",
            ],
            StageBuilder.From(parsed).Validate().Select(p => p.ToString()));
    }

    [Fact]
    public void Parse_still_throws_InvalidDataException_for_a_damaged_file() =>
        Assert.IsType<InvalidDataException>(Record.Exception(() => Stage.Parse(new byte[Stage.FileSize + 1])));

    [Fact]
    public void The_public_api_is_exactly_the_documented_types() =>
        Assert.Equal(
            [
                "Board", "Cell", "CellKind", "Character", "Direction", "Directions", "Move", "Stage", "StageBuilder",
                "StageDocument", "StageFormatException", "StageProblem", "StepResult", "Tile",
            ],
            typeof(Board).Assembly.GetExportedTypes().Select(t => t.Name).Order());

    private static StepResult? Apply(Board board, string input)
    {
        switch (input)
        {
            case "Switch":
                board.Switch();
                return null;
            case "Undo":
                board.Undo();
                return null;
            case "Retry":
                board.Retry();
                return null;
            default:
                return board.Step(input switch
                {
                    "+X" => Direction.PlusX,
                    "-X" => Direction.MinusX,
                    "+Y" => Direction.PlusY,
                    _ => Direction.MinusY,
                });
        }
    }

    private static string Snapshot(Board board)
    {
        var text = new StringBuilder();
        for (var y = 0; y < Stage.Height; y++)
        {
            for (var x = 0; x < Stage.Width; x++)
            {
                text.Append(board[x, y].ToString("X4")).Append(' ');
            }
        }

        text.AppendJoin(';', board.Characters).Append($"|{board.ActiveIndex}|{board.IsCleared}|{board.CanUndo}|");
        if (board.LastMove is { } move)
        {
            text.Append(move with { Landings = [] }).AppendJoin(',', move.Landings);
        }

        return text.ToString();
    }

    private static void WriteCell(byte[] file, int x, int y, ushort cell) =>
        BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(28 + (((y * Stage.Width) + x) * 2)), cell);
}
