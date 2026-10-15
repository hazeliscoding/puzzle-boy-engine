namespace PuzzleBoy.Rules;

/// <summary>One cell of an authored stage, in the terms an editor shows instead of raw bits.</summary>
/// <param name="Kind">What the cell holds. <see cref="CellKind.Hole"/> is floor with the hole flag.</param>
/// <param name="Group">The block or swivel group, 1 to 2047. Every other kind has 0.</param>
/// <param name="OverHole">True when a block or swivel part rests on a hole. Every other kind has false.</param>
public readonly record struct Tile(CellKind Kind, int Group = 0, bool OverHole = false)
{
    /// <summary>The highest group a cell can hold in its low 11 bits.</summary>
    internal const int MaxGroup = 0x07FF;

    /// <summary>True for the kinds that belong to a group and can rest on a hole.</summary>
    internal bool IsPart => Kind is CellKind.Block or CellKind.SwivelCenter or CellKind.SwivelArm;

    /// <summary>The raw cell value the original stores for this tile; see <see cref="Cell"/>.</summary>
    /// <exception cref="InvalidOperationException">
    /// The tile can't be stored: an unknown kind, a block or swivel part without a group from 1 to 2047, or a plain
    /// tile with a group or over a hole. <see cref="StageBuilder.Validate"/> reports each of these.
    /// </exception>
    public ushort ToCell()
    {
        if (!IsPart)
        {
            if (Group != 0 || OverHole)
            {
                throw new InvalidOperationException($"A {Kind} tile has no group and can't rest on a hole.");
            }

            return Kind switch
            {
                CellKind.Floor => 0x0000,
                CellKind.Wall => 0x2000,
                CellKind.Hole => Cell.HoleFlag,
                _ => throw new InvalidOperationException($"{(int)Kind} isn't a tile kind."),
            };
        }

        if (Group is < 1 or > MaxGroup)
        {
            throw new InvalidOperationException($"A {Kind} tile needs a group from 1 to {MaxGroup}, not {Group}.");
        }

        var type = Kind switch
        {
            CellKind.Block => 0x6000,
            CellKind.SwivelCenter => 0x8000,
            _ => 0xA000,
        };
        return (ushort)(type | (OverHole ? Cell.HoleFlag : 0) | Group);
    }

    /// <summary>
    /// The tile a raw cell value shows. Bits a tile doesn't model are dropped: a block's runtime core type, a wall's
    /// other bits and the unused <c>0x1000</c> bit.
    /// </summary>
    public static Tile FromCell(ushort cell)
    {
        var tile = new Tile(Cell.KindOf(cell));
        return tile.IsPart ? tile with { Group = Cell.GroupOf(cell), OverHole = (cell & Cell.HoleFlag) != 0 } : tile;
    }
}
