namespace PuzzleBoy.Rules.Tests;

public sealed class TileTests
{
    public static TheoryData<int, Tile> Cells => new()
    {
        { 0x0000, new Tile(CellKind.Floor) },
        { 0x2000, new Tile(CellKind.Wall) },
        { 0x0800, new Tile(CellKind.Hole) },
        { 0x6001, new Tile(CellKind.Block, 1) },
        { 0x6FFF, new Tile(CellKind.Block, 2047, OverHole: true) },
        { 0x8020, new Tile(CellKind.SwivelCenter, 32) },
        { 0x8820, new Tile(CellKind.SwivelCenter, 32, OverHole: true) },
        { 0xA021, new Tile(CellKind.SwivelArm, 33) },
        { 0xA820, new Tile(CellKind.SwivelArm, 32, OverHole: true) },
    };

    [Theory]
    [MemberData(nameof(Cells))]
    public void A_tile_and_its_raw_cell_convert_both_ways(int cell, Tile tile)
    {
        Assert.Equal((ushort)cell, tile.ToCell());
        Assert.Equal(tile, Tile.FromCell((ushort)cell));
    }

    [Theory]
    [InlineData(0x4005, CellKind.Block, 5)]
    [InlineData(0x2800, CellKind.Wall, 0)]
    [InlineData(0x1000, CellKind.Floor, 0)]
    [InlineData(0xE000, CellKind.Wall, 0)]
    public void Bits_a_tile_does_not_model_come_back_canonical(int cell, CellKind kind, int group) =>
        Assert.Equal(new Tile(kind, group), Tile.FromCell((ushort)cell));

    [Theory]
    [InlineData(CellKind.Floor, 3, false)]
    [InlineData(CellKind.Wall, 0, true)]
    [InlineData(CellKind.Block, 0, false)]
    [InlineData(CellKind.SwivelArm, 2048, false)]
    [InlineData((CellKind)42, 0, false)]
    public void A_tile_the_builder_rejects_cannot_become_a_cell(CellKind kind, int group, bool overHole) =>
        Assert.Throws<InvalidOperationException>(() => new Tile(kind, group, overHole).ToCell());
}
