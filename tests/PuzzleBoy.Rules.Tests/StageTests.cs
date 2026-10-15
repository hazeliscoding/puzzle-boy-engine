using System.Buffers.Binary;

namespace PuzzleBoy.Rules.Tests;

public sealed class StageTests
{
    private const ushort Wall = 0x2000;
    private const ushort Block = 0x600A;

    [Fact]
    public void Parse_reads_characters_goal_and_cells()
    {
        var stage = Stage.Parse(StageFile(enabled: [0], starts: [(17, 2)], goal: (2, 2), cells: c => c[(5 * Stage.Width) + 3] = Block));

        Assert.Equal([(0, 17, 2)], stage.Characters);
        Assert.Equal((2, 2), stage.Goal);
        Assert.Equal(Block, stage[3, 5]);
        Assert.Equal(Wall, stage[0, 0]);
    }

    [Fact]
    public void Parse_picks_characters_by_enable_byte_and_keeps_their_slot()
    {
        var stage = Stage.Parse(StageFile(enabled: [2, 3], starts: [(6, 8), (6, 7)], goal: (7, 13)));

        Assert.Equal([(2, 6, 8), (3, 6, 7)], stage.Characters);
    }

    [Fact]
    public void Parse_undoes_crlf_damage()
    {
        // 10 is 0A, the byte a CRLF conversion rewrites: in a start position, the goal and a block group.
        var file = StageFile(enabled: [0], starts: [(10, 3)], goal: (4, 10), cells: c => c[(3 * Stage.Width) + 5] = Block);
        var damaged = file.SelectMany(b => b == 0x0A ? new byte[] { 0x0D, 0x0A } : [b]).ToArray();
        Assert.True(damaged.Length > Stage.FileSize);

        var stage = Stage.Parse(damaged);

        Assert.Equal([(0, 10, 3)], stage.Characters);
        Assert.Equal((4, 10), stage.Goal);
        Assert.Equal(Block, stage[5, 3]);
    }

    [Fact]
    public void Parse_rejects_a_file_of_the_wrong_size() =>
        Assert.Throws<InvalidDataException>(() => Stage.Parse(new byte[Stage.FileSize - 1]));

    [Fact]
    public void Parse_rejects_a_count_that_disagrees_with_the_enable_bytes()
    {
        var file = StageFile(enabled: [0, 1], starts: [(1, 1), (2, 2)], goal: (3, 3));
        BinaryPrimitives.WriteInt32LittleEndian(file, 1);

        Assert.Throws<InvalidDataException>(() => Stage.Parse(file));
    }

    [Fact]
    public void Parse_rejects_a_position_outside_the_grid() =>
        Assert.Throws<InvalidDataException>(() => Stage.Parse(StageFile(enabled: [0], starts: [(20, 1)], goal: (3, 3))));

    /// <summary>A handmade stage file: walls everywhere, one start position per enabled slot.</summary>
    private static byte[] StageFile(int[] enabled, (short X, short Y)[] starts, (short X, short Y) goal, Action<ushort[]>? cells = null)
    {
        var grid = Enumerable.Repeat(Wall, Stage.Width * Stage.Height).ToArray();
        cells?.Invoke(grid);

        var file = new byte[Stage.FileSize];
        BinaryPrimitives.WriteInt32LittleEndian(file, enabled.Length);
        for (var i = 0; i < enabled.Length; i++)
        {
            file[4 + enabled[i]] = 1;
            BinaryPrimitives.WriteInt16LittleEndian(file.AsSpan(8 + (enabled[i] * 4)), starts[i].X);
            BinaryPrimitives.WriteInt16LittleEndian(file.AsSpan(10 + (enabled[i] * 4)), starts[i].Y);
        }

        BinaryPrimitives.WriteInt16LittleEndian(file.AsSpan(24), goal.X);
        BinaryPrimitives.WriteInt16LittleEndian(file.AsSpan(26), goal.Y);
        for (var i = 0; i < grid.Length; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(28 + (i * 2)), grid[i]);
        }

        return file;
    }
}
