using System.Buffers.Binary;

namespace PuzzleBoy.Rules.Tests;

/// <summary>
/// Handmade stages drawn as text, so rule tests read like the board. Column is x and row is y, so
/// <see cref="Direction.PlusX"/> moves right and <see cref="Direction.PlusY"/> moves down the drawing.
/// The game shows +y up the screen, so on screen a drawing appears mirrored top to bottom.
/// Everything outside the drawing is wall.
/// </summary>
/// <remarks>
/// Legend: <c>#</c> wall, <c>.</c> floor, <c>o</c> hole, <c>G</c> goal, <c>1</c>–<c>4</c> characters in slots 0–3,
/// lowercase letters block groups, <c>C</c>/<c>R</c> center and arms of one swivel, <c>D</c>/<c>S</c> of a second,
/// and <c>H</c> an arm of the first swivel resting on a hole.
/// </remarks>
internal static class Ascii
{
    private static readonly Dictionary<char, ushort> Cells = new()
    {
        ['#'] = 0x2000,
        ['.'] = 0x0000,
        ['o'] = 0x0800,
        ['C'] = 0x8020,
        ['R'] = 0xA020,
        ['H'] = 0xA820,
        ['D'] = 0x8021,
        ['S'] = 0xA021,
    };

    public static Board Board(string drawing) => new(Stage(drawing));

    public static Stage Stage(string drawing)
    {
        var rows = drawing.Split('\n');
        var grid = Enumerable.Repeat((ushort)0x2000, Rules.Stage.Width * Rules.Stage.Height).ToArray();
        var starts = new (int X, int Y)?[4];
        var goal = (X: 19, Y: 19);
        for (var y = 0; y < rows.Length; y++)
        {
            for (var x = 0; x < rows[y].Length; x++)
            {
                var c = rows[y][x];
                grid[(y * Rules.Stage.Width) + x] = CellFor(c);
                if (c is >= '1' and <= '4')
                {
                    starts[c - '1'] = (x, y);
                }
                else if (c == 'G')
                {
                    goal = (x, y);
                }
            }
        }

        var file = new byte[Rules.Stage.FileSize];
        BinaryPrimitives.WriteInt32LittleEndian(file, starts.Count(s => s is not null));
        for (var slot = 0; slot < starts.Length; slot++)
        {
            if (starts[slot] is { } start)
            {
                file[4 + slot] = 1;
                BinaryPrimitives.WriteInt16LittleEndian(file.AsSpan(8 + (slot * 4)), (short)start.X);
                BinaryPrimitives.WriteInt16LittleEndian(file.AsSpan(10 + (slot * 4)), (short)start.Y);
            }
        }

        BinaryPrimitives.WriteInt16LittleEndian(file.AsSpan(24), (short)goal.X);
        BinaryPrimitives.WriteInt16LittleEndian(file.AsSpan(26), (short)goal.Y);
        for (var i = 0; i < grid.Length; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(28 + (i * 2)), grid[i]);
        }

        return Rules.Stage.Parse(file);
    }

    /// <summary>Draws the board the way <see cref="Stage(string)"/> reads it, over the same width and height as <paramref name="like"/>.</summary>
    public static string Draw(Board board, string like)
    {
        var rows = like.Split('\n');
        var lines = new List<string>();
        for (var y = 0; y < rows.Length; y++)
        {
            var line = new char[rows[y].Length];
            for (var x = 0; x < line.Length; x++)
            {
                var character = board.Characters.FirstOrDefault(c => c.Visible && c.X == x && c.Y == y);
                line[x] = character.Visible ? (char)('1' + character.Slot)
                    : board.Goal == (x, y) && board[x, y] == 0 ? 'G'
                    : CharFor(board[x, y]);
            }

            lines.Add(new string(line));
        }

        return string.Join('\n', lines);
    }

    private static ushort CellFor(char c) => c switch
    {
        _ when Cells.TryGetValue(c, out var cell) => cell,
        'G' or (>= '1' and <= '4') => 0x0000,
        >= 'a' and <= 'z' => (ushort)(0x6000 | (c - 'a' + 1)),
        _ => throw new ArgumentException($"No cell is drawn as '{c}'."),
    };

    private static char CharFor(ushort cell)
    {
        foreach (var (c, value) in Cells)
        {
            if (value == cell)
            {
                return c;
            }
        }

        return Cell.KindOf(cell) == CellKind.Block ? (char)('a' + Cell.GroupOf(cell) - 1) : '?';
    }
}
