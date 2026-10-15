namespace PuzzleBoy.Rules.Tests;

public sealed class CellTests
{
    [Theory]
    [InlineData(0x0000, CellKind.Floor)]
    [InlineData(0x1000, CellKind.Floor)]
    [InlineData(0x0800, CellKind.Hole)]
    [InlineData(0x2000, CellKind.Wall)]
    [InlineData(0x2800, CellKind.Wall)]
    [InlineData(0xC000, CellKind.Wall)]
    [InlineData(0xE000, CellKind.Wall)]
    [InlineData(0x4005, CellKind.Block)]
    [InlineData(0x607D, CellKind.Block)]
    [InlineData(0x8001, CellKind.SwivelCenter)]
    [InlineData(0xA001, CellKind.SwivelArm)]
    [InlineData(0xA802, CellKind.SwivelArm)]
    public void KindOf_reads_the_type_from_bits_13_to_15(int cell, CellKind kind) =>
        Assert.Equal(kind, Cell.KindOf((ushort)cell));

    [Theory]
    [InlineData(0x607D, 0x7D)]
    [InlineData(0x6123, 0x123)]
    [InlineData(0xA802, 0x002)]
    [InlineData(0x0FFF, 0x7FF)]
    public void GroupOf_reads_the_low_11_bits_below_the_hole_flag(int cell, int group) =>
        Assert.Equal(group, Cell.GroupOf((ushort)cell));
}
