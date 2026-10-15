namespace PuzzleBoy.Rules;

/// <summary>What occupies a cell of the stage grid.</summary>
public enum CellKind
{
    /// <summary>Open floor a character can walk on.</summary>
    Floor,

    /// <summary>A fixed wall. The grid outside the play area is all wall.</summary>
    Wall,

    /// <summary>A hole. Characters can't cross it until a block fills it.</summary>
    Hole,

    /// <summary>Part of a pushable block. Cells with the same group form one block.</summary>
    Block,

    /// <summary>The axis a swivel block turns around.</summary>
    SwivelCenter,

    /// <summary>An arm of a swivel block. It turns with the center of the same group.</summary>
    SwivelArm,
}

/// <summary>Reads the raw 16-bit cell values of a <see cref="Stage"/>.</summary>
public static class Cell
{
    /// <summary>The flag that marks a hole, under whatever else the cell holds.</summary>
    public const ushort HoleFlag = 0x0800;

    /// <summary>
    /// What a raw cell value holds, from its type in bits 13 to 15. A block or swivel part keeps its kind
    /// when it rests on a hole; the hole flag beneath it is read separately.
    /// </summary>
    public static CellKind KindOf(ushort cell) => (cell >> 13) switch
    {
        0 => (cell & HoleFlag) != 0 ? CellKind.Hole : CellKind.Floor,
        // Type 2 is a block group's core cell. The original assigns it when a stage loads; stage files only use 3.
        2 or 3 => CellKind.Block,
        4 => CellKind.SwivelCenter,
        5 => CellKind.SwivelArm,
        // Type 1 is a wall, and the original treats the unused types 6 and 7 as solid too.
        _ => CellKind.Wall,
    };

    /// <summary>The block or swivel group a cell belongs to: its low 11 bits, below the hole flag.</summary>
    public static int GroupOf(ushort cell) => cell & 0x07FF;
}
