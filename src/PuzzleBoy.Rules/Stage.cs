using System.Buffers.Binary;

namespace PuzzleBoy.Rules;

/// <summary>A stage as the original game stores it in its <c>pbMapNN.map</c> files.</summary>
public sealed class Stage
{
    /// <summary>Columns in every stage's grid.</summary>
    public const int Width = 20;

    /// <summary>Rows in every stage's grid.</summary>
    public const int Height = 20;

    /// <summary>The size of an undamaged stage file, in bytes.</summary>
    public const int FileSize = 828;

    private const int Slots = 4;
    private const int GoalOffset = 24;
    private const int GridOffset = 28;

    private readonly ushort[] _cells;

    internal Stage(IReadOnlyList<(int Slot, int X, int Y)> characters, (int X, int Y) goal, ushort[] cells)
    {
        Characters = characters;
        Goal = goal;
        _cells = cells;
    }

    /// <summary>The characters' start positions, in slot order. Slots can be skipped.</summary>
    public IReadOnlyList<(int Slot, int X, int Y)> Characters { get; }

    /// <summary>The goal's position: the spinning blue cube.</summary>
    public (int X, int Y) Goal { get; }

    /// <summary>The raw cell value: kind flags in the high byte, block or swivel group in the low byte.</summary>
    public ushort this[int x, int y] => _cells[(y * Width) + x];

    /// <summary>Reads a stage file as it ships in the <c>pazzleboy</c> bundle.</summary>
    /// <param name="file">The file's bytes. One longer than <see cref="FileSize"/> is taken to carry the CRLF damage and is repaired first.</param>
    /// <exception cref="InvalidDataException">
    /// The bytes aren't a stage: the wrong size, no enabled character, a count that disagrees with the enable bytes, or a position off the grid.
    /// </exception>
    public static Stage Parse(ReadOnlySpan<byte> file)
    {
        if (file.Length > FileSize)
        {
            file = UndoCrlf(file);
        }

        if (file.Length != FileSize)
        {
            throw new InvalidDataException($"A stage file is {FileSize} bytes, but this one is {file.Length}.");
        }

        var characters = new List<(int Slot, int X, int Y)>();
        for (var slot = 0; slot < Slots; slot++)
        {
            // The enable byte decides, not the order: some stages use only slot 1, or slots 2 and 3.
            if (file[4 + slot] != 0)
            {
                var (x, y) = ReadPosition(file, 8 + (slot * 4));
                characters.Add((slot, x, y));
            }
        }

        var count = BinaryPrimitives.ReadInt32LittleEndian(file);
        if (characters.Count == 0 || characters.Count != count)
        {
            throw new InvalidDataException($"The stage says it has {count} characters but enables {characters.Count}.");
        }

        var cells = new ushort[Width * Height];
        for (var i = 0; i < cells.Length; i++)
        {
            cells[i] = BinaryPrimitives.ReadUInt16LittleEndian(file[(GridOffset + (i * 2))..]);
        }

        return new Stage(characters, ReadPosition(file, GoalOffset), cells);
    }

    /// <summary>
    /// The stage in the original's 828-byte layout: the character count, the enable bytes, the starts, the goal and the
    /// grid. <see cref="Parse"/> reads it back with the same characters, goal and cells.
    /// </summary>
    public byte[] ToFile()
    {
        var file = new byte[FileSize];
        BinaryPrimitives.WriteInt32LittleEndian(file, Characters.Count);
        foreach (var (slot, x, y) in Characters)
        {
            file[4 + slot] = 1;
            WritePosition(file, 8 + (slot * 4), x, y);
        }

        WritePosition(file, GoalOffset, Goal.X, Goal.Y);
        for (var i = 0; i < _cells.Length; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(GridOffset + (i * 2)), _cells[i]);
        }

        return file;
    }

    private static void WritePosition(byte[] file, int offset, int x, int y)
    {
        BinaryPrimitives.WriteInt16LittleEndian(file.AsSpan(offset), (short)x);
        BinaryPrimitives.WriteInt16LittleEndian(file.AsSpan(offset + 2), (short)y);
    }

    private static (int X, int Y) ReadPosition(ReadOnlySpan<byte> file, int offset)
    {
        int x = BinaryPrimitives.ReadInt16LittleEndian(file[offset..]);
        int y = BinaryPrimitives.ReadInt16LittleEndian(file[(offset + 2)..]);
        if (x is < 0 or >= Width || y is < 0 or >= Height)
        {
            throw new InvalidDataException($"The position ({x}, {y}) at byte {offset} is outside the {Width}x{Height} grid.");
        }

        return (x, y);
    }

    // Six of the shipped stage files had every 0A byte rewritten as 0D 0A by a line-ending conversion.
    private static byte[] UndoCrlf(ReadOnlySpan<byte> file)
    {
        var repaired = new List<byte>(file.Length);
        for (var i = 0; i < file.Length; i++)
        {
            if (file[i] == 0x0D && i + 1 < file.Length && file[i + 1] == 0x0A)
            {
                continue;
            }

            repaired.Add(file[i]);
        }

        return [.. repaired];
    }
}
