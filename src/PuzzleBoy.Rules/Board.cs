namespace PuzzleBoy.Rules;

/// <summary>A step on the stage grid, numbered the way the original numbers its directions.</summary>
public enum Direction
{
    /// <summary>One cell toward larger y.</summary>
    PlusY,

    /// <summary>One cell toward larger x.</summary>
    PlusX,

    /// <summary>One cell toward smaller y.</summary>
    MinusY,

    /// <summary>One cell toward smaller x.</summary>
    MinusX,
}

/// <summary>Helpers for <see cref="Direction"/>.</summary>
public static class Directions
{
    /// <summary>The (x, y) step one cell in <paramref name="direction"/>.</summary>
    public static (int X, int Y) Offset(this Direction direction) => direction switch
    {
        Direction.PlusY => (0, 1),
        Direction.PlusX => (1, 0),
        Direction.MinusY => (0, -1),
        _ => (-1, 0),
    };
}

/// <summary>What a step did.</summary>
public enum StepResult
{
    /// <summary>The character walked one cell.</summary>
    Walked,

    /// <summary>The character pushed a block one cell and followed it. The block may have fallen into holes.</summary>
    Pushed,

    /// <summary>The character turned a swivel a quarter turn and moved one or two cells.</summary>
    Turned,

    /// <summary>Nothing changed. The original makes no distinction between a bump and a refused push or turn.</summary>
    Refused,
}

/// <summary>What a successful step moved, so the game can animate it; the board has already changed.</summary>
/// <param name="Result">Walked, pushed or turned; never refused.</param>
/// <param name="Mover">The index into <see cref="Board.Characters"/> of the character that moved. It may no longer be active.</param>
/// <param name="Direction">The direction of the step.</param>
/// <param name="Cells">How far the character moved: 1, or 2 for a swivel's two-cell step.</param>
/// <param name="Group">The pushed or turned group's id; 0 for a walk.</param>
/// <param name="Landings">Where the group's cells are now; empty for a walk. After a fall these cells are plain floor.</param>
/// <param name="Center">The swivel's center, for a turn.</param>
/// <param name="Turn">For a turn, -1 clockwise or +1 counterclockwise, with x right and y up; 0 otherwise.</param>
/// <param name="Fell">True when a pushed block fell into the holes under it.</param>
public sealed record Move(
    StepResult Result, int Mover, Direction Direction, int Cells, int Group,
    IReadOnlyList<(int X, int Y)> Landings, (int X, int Y) Center, int Turn, bool Fell);

/// <summary>A character on the board. A character that reaches the goal while others are still out is hidden.</summary>
/// <param name="Slot">The stage file's slot, 0 to 3.</param>
/// <param name="X">The column.</param>
/// <param name="Y">The row.</param>
/// <param name="Visible">False once the character has reached the goal and left the board.</param>
public readonly record struct Character(int Slot, int X, int Y, bool Visible);

/// <summary>
/// A stage in play: the cells, the characters, the goal and the undo history. Steps apply the original's rules
/// (ROADMAP.md, "M0 findings") instantly; animation and input timing belong to the game.
/// </summary>
public sealed class Board
{
    private const int UndoLimit = 30;
    private const ushort PlainWall = 0x2000;

    // When a block or swivel part leaves a cell, the cell keeps its hole flag and the unused bit 0x1000.
    private const ushort Underneath = 0x1800;

    private readonly Stage _stage;
    private readonly ushort[] _cells = new ushort[Stage.Width * Stage.Height];
    private readonly Character[] _characters;
    private readonly List<(ushort[] Cells, Character[] Characters, int Active)> _history = [];
    private int _active;

    /// <summary>Starts <paramref name="stage"/> from its file layout, with the first character in control.</summary>
    public Board(Stage stage)
    {
        _stage = stage;
        _characters = new Character[stage.Characters.Count];
        Retry();
    }

    /// <summary>The raw cell value at (<paramref name="x"/>, <paramref name="y"/>); see <see cref="Cell"/>.</summary>
    public ushort this[int x, int y] => _cells[Index(x, y)];

    /// <summary>Every character, in slot order, including those that left at the goal.</summary>
    public IReadOnlyList<Character> Characters => _characters;

    /// <summary>The character the player controls.</summary>
    public Character Active => _characters[_active];

    /// <summary>The index of <see cref="Active"/> in <see cref="Characters"/>.</summary>
    public int ActiveIndex => _active;

    /// <summary>The goal's position.</summary>
    public (int X, int Y) Goal => _stage.Goal;

    /// <summary>True once the last character has reached the goal. A cleared board refuses every step.</summary>
    public bool IsCleared { get; private set; }

    /// <summary>Whether a push or turn can be undone. Plain walking is never recorded.</summary>
    public bool CanUndo => _history.Count > 0;

    /// <summary>What the last <see cref="Step"/> moved; null after a refused step, a switch, an undo or a retry.</summary>
    public Move? LastMove { get; private set; }

    /// <summary>Moves the active character one step, pushing a block or turning a swivel if that's what it walks into.</summary>
    public StepResult Step(Direction direction)
    {
        LastMove = null;
        var me = Active;
        if (IsCleared || (me.X, me.Y) == Goal)
        {
            return StepResult.Refused;
        }

        var (dx, dy) = direction.Offset();
        var (x, y) = (me.X + dx, me.Y + dy);
        if (!InGrid(x, y) || OtherCharacterAt(x, y) || (this[x, y] & Cell.HoleFlag) != 0)
        {
            return StepResult.Refused;
        }

        switch (Cell.KindOf(this[x, y]))
        {
            case CellKind.Floor:
                LastMove = new Move(StepResult.Walked, _active, direction, 1, 0, [], default, 0, false);
                MoveActive(dx, dy, 1);
                return StepResult.Walked;
            case CellKind.Block:
                return Push(direction, Cell.GroupOf(this[x, y]));
            case CellKind.SwivelArm:
                return Turn(direction, Cell.GroupOf(this[x, y]));
            default:
                return StepResult.Refused;
        }
    }

    /// <summary>Goes back to the moment before the last push or turn, discarding any walking since. Returns false when there's nothing to undo.</summary>
    public bool Undo()
    {
        if (_history.Count == 0)
        {
            return false;
        }

        var (cells, characters, active) = _history[^1];
        _history.RemoveAt(_history.Count - 1);
        LastMove = null;
        cells.CopyTo(_cells, 0);
        characters.CopyTo(_characters, 0);
        _active = active;
        return true;
    }

    /// <summary>
    /// Hands control to the next visible character, in the order a goal arrival uses. The original has no switch; the
    /// port adds it for the unused multi-character stages, which can't be finished without one (ROADMAP.md, M4).
    /// It isn't recorded, so undo still returns control to the character that made the undone push or turn.
    /// Returns false, changing nothing, on a cleared board or when only one character is left.
    /// </summary>
    public bool Switch()
    {
        LastMove = null;
        if (IsCleared || _characters.Count(c => c.Visible) < 2)
        {
            return false;
        }

        PassControl();
        return true;
    }

    /// <summary>Restarts the stage from its file layout and wipes the undo history.</summary>
    public void Retry()
    {
        for (var y = 0; y < Stage.Height; y++)
        {
            for (var x = 0; x < Stage.Width; x++)
            {
                _cells[Index(x, y)] = _stage[x, y];
            }
        }

        for (var i = 0; i < _characters.Length; i++)
        {
            var (slot, x, y) = _stage.Characters[i];
            _characters[i] = new Character(slot, x, y, true);
        }

        _active = 0;
        IsCleared = false;
        _history.Clear();
        LastMove = null;
    }

    private StepResult Push(Direction direction, int group)
    {
        var (dx, dy) = direction.Offset();
        var parts = PartsOf(group);
        foreach (var (x, y) in parts)
        {
            var (tx, ty) = (x + dx, y + dy);
            if (!InGrid(tx, ty) || OtherCharacterAt(tx, ty) || !(this[tx, ty] >> 13 == 0 || IsPartOf(this[tx, ty], group)))
            {
                return StepResult.Refused;
            }
        }

        Record();
        var landings = parts.Select(p => (p.X + dx, p.Y + dy)).ToList();
        Shift(parts, landings);

        // A block falls only when every one of its cells is over a hole; the holes become plain floor.
        var fell = landings.TrueForAll(l => (_cells[Index(l.Item1, l.Item2)] & Cell.HoleFlag) != 0);
        if (fell)
        {
            foreach (var (x, y) in landings)
            {
                _cells[Index(x, y)] &= 0x1000;
            }
        }

        LastMove = new Move(StepResult.Pushed, _active, direction, 1, group, landings, default, 0, fell);
        MoveActive(dx, dy, 1);
        return StepResult.Pushed;
    }

    private StepResult Turn(Direction direction, int group)
    {
        var parts = PartsOf(group);
        var me = Active;
        var center = parts.Find(p => Cell.KindOf(this[p.X, p.Y]) == CellKind.SwivelCenter);
        if (!parts.Exists(p => Cell.KindOf(this[p.X, p.Y]) == CellKind.SwivelCenter) || me.X == center.X || me.Y == center.Y)
        {
            return StepResult.Refused;
        }

        // The original picks the sense from which side of the center the character stands on;
        // either way the pushed arm ends up on the side the push points to.
        var turn = direction switch
        {
            Direction.PlusY => me.X < center.X ? -1 : 1,
            Direction.PlusX => me.Y < center.Y ? 1 : -1,
            Direction.MinusY => me.X < center.X ? 1 : -1,
            _ => me.Y < center.Y ? -1 : 1,
        };

        var (dx, dy) = direction.Offset();
        var steps = IsPartAt(me.X + dy, me.Y + dx, group) || IsPartAt(me.X - dy, me.Y - dx, group) ? 2 : 1;
        for (var i = 0; i <= steps; i++)
        {
            var (px, py) = (me.X + (i * dx), me.Y + (i * dy));
            if (!InGrid(px, py) || (this[px, py] & Cell.HoleFlag) != 0 || this[px, py] == PlainWall)
            {
                return StepResult.Refused;
            }
        }

        var landings = new List<(int, int)>();
        foreach (var (x, y) in parts)
        {
            var (ox, oy) = (x - center.X, y - center.Y);
            var (rx, ry) = turn == -1 ? (oy, -ox) : (-oy, ox);
            var land = (center.X + rx, center.Y + ry);
            var sweep = (center.X + ox + rx, center.Y + oy + ry);
            if (BlocksTurn(land, group) || BlocksTurn(sweep, group))
            {
                return StepResult.Refused;
            }

            landings.Add(land);
        }

        Record();
        Shift(parts, landings);
        LastMove = new Move(StepResult.Turned, _active, direction, steps, group, landings, center, turn, false);
        MoveActive(dx, dy, steps);
        return StepResult.Turned;
    }

    private bool BlocksTurn((int X, int Y) cell, int group)
    {
        if (!InGrid(cell.X, cell.Y) || OtherCharacterAt(cell.X, cell.Y))
        {
            return true;
        }

        var value = this[cell.X, cell.Y];
        return value >> 13 == 1 || (value >> 13 >= 2 && Cell.GroupOf(value) != group);
    }

    private void MoveActive(int dx, int dy, int steps)
    {
        var me = Active;
        _characters[_active] = me with { X = me.X + (dx * steps), Y = me.Y + (dy * steps) };
        if ((Active.X, Active.Y) != Goal)
        {
            return;
        }

        if (_characters.Count(c => c.Visible) < 2)
        {
            IsCleared = true;
            _history.Clear();
            return;
        }

        _characters[_active] = Active with { Visible = false };
        PassControl();
    }

    /// <summary>Moves control to the next visible character in ascending slot order, wrapping around.</summary>
    private void PassControl()
    {
        do
        {
            _active = (_active + 1) % _characters.Length;
        }
        while (!_characters[_active].Visible);
    }

    /// <summary>Moves a group's parts to <paramref name="landings"/>, leaving each old cell's hole flag behind and keeping each new cell's.</summary>
    private void Shift(List<(int X, int Y)> parts, List<(int, int)> landings)
    {
        var values = parts.Select(p => (ushort)(this[p.X, p.Y] & ~Underneath)).ToList();
        foreach (var (x, y) in parts)
        {
            _cells[Index(x, y)] &= Underneath;
        }

        for (var i = 0; i < landings.Count; i++)
        {
            var (x, y) = landings[i];
            _cells[Index(x, y)] = (ushort)((_cells[Index(x, y)] & Underneath) | values[i]);
        }
    }

    private void Record()
    {
        _history.Add(((ushort[])_cells.Clone(), (Character[])_characters.Clone(), _active));
        if (_history.Count > UndoLimit)
        {
            _history.RemoveAt(0);
        }
    }

    /// <summary>Every block or swivel cell of <paramref name="group"/>. The original keys groups by id alone, so the cells needn't touch.</summary>
    private List<(int X, int Y)> PartsOf(int group)
    {
        var parts = new List<(int X, int Y)>();
        for (var y = 0; y < Stage.Height; y++)
        {
            for (var x = 0; x < Stage.Width; x++)
            {
                if (IsPartOf(this[x, y], group))
                {
                    parts.Add((x, y));
                }
            }
        }

        return parts;
    }

    private bool IsPartAt(int x, int y, int group) => InGrid(x, y) && IsPartOf(this[x, y], group);

    private static bool IsPartOf(ushort cell, int group) => cell >> 13 is >= 2 and <= 5 && Cell.GroupOf(cell) == group;

    private bool OtherCharacterAt(int x, int y)
    {
        for (var i = 0; i < _characters.Length; i++)
        {
            if (i != _active && _characters[i].Visible && _characters[i].X == x && _characters[i].Y == y)
            {
                return true;
            }
        }

        return false;
    }

    private static bool InGrid(int x, int y) => x is >= 0 and < Stage.Width && y is >= 0 and < Stage.Height;

    private static int Index(int x, int y) => (y * Stage.Width) + x;
}
