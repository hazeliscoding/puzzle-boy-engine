namespace PuzzleBoy.Rules;

/// <summary>
/// A stage being authored: a 20×20 grid of <see cref="Tile"/>s that starts as all wall, a goal and the characters.
/// <see cref="Build"/> checks it against the rules for authored stages; <see cref="Stage.Parse"/> stays lenient.
/// </summary>
public sealed class StageBuilder
{
    private const int MaxCharacters = 4;

    private readonly Tile[] _tiles = [.. Enumerable.Repeat(new Tile(CellKind.Wall), Stage.Width * Stage.Height)];

    /// <summary>The tile at column <paramref name="x"/> and row <paramref name="y"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The cell is off the grid.</exception>
    public Tile this[int x, int y]
    {
        get => _tiles[Index(x, y)];
        set => _tiles[Index(x, y)] = value;
    }

    /// <summary>The goal's position, or null before one is placed.</summary>
    public (int X, int Y)? Goal { get; set; }

    /// <summary>The characters' start positions. The index is the slot: the first one starts in control.</summary>
    public List<(int X, int Y)> Characters { get; } = [];

    /// <summary>
    /// A builder holding <paramref name="stage"/>'s tiles, goal and characters. Characters take slots 0 up in their
    /// order, so a stage that skips slots comes back without the gap; see <see cref="Tile.FromCell"/> for the cells.
    /// </summary>
    public static StageBuilder From(Stage stage)
    {
        var builder = new StageBuilder { Goal = stage.Goal };
        for (var y = 0; y < Stage.Height; y++)
        {
            for (var x = 0; x < Stage.Width; x++)
            {
                builder[x, y] = Tile.FromCell(stage[x, y]);
            }
        }

        builder.Characters.AddRange(stage.Characters.Select(c => (c.X, c.Y)));
        return builder;
    }

    /// <summary>Everything that stops this stage from being built, in a stable order; empty when it can be.</summary>
    public IReadOnlyList<StageProblem> Validate()
    {
        var problems = new List<StageProblem>();
        CheckGoal(problems);
        CheckCharacters(problems);
        var groups = CheckTiles(problems);
        foreach (var (group, cells) in groups.OrderBy(g => g.Key))
        {
            var block = cells.FindIndex(c => this[c.X, c.Y].Kind == CellKind.Block);
            var swivel = cells.FindIndex(c => this[c.X, c.Y].Kind != CellKind.Block);
            if (block >= 0 && swivel >= 0)
            {
                problems.Add(new($"Group {group} mixes blocks and swivel parts: there's a block at ({cells[block].X}, {cells[block].Y}). Give the swivel its own group.", cells[swivel].X, cells[swivel].Y));
            }
            else if (block >= 0)
            {
                CheckBlock(problems, group, cells);
            }
            else
            {
                CheckSwivel(problems, group, cells);
            }
        }

        return problems;
    }

    /// <summary>The stage, characters in slot order.</summary>
    /// <exception cref="StageFormatException">The stage breaks a rule; it carries every problem <see cref="Validate"/> finds.</exception>
    public Stage Build()
    {
        var problems = Validate();
        if (problems.Count > 0)
        {
            throw new StageFormatException(problems);
        }

        return new Stage(
            [.. Characters.Select((c, slot) => (slot, c.X, c.Y))],
            Goal!.Value,
            [.. _tiles.Select(t => t.ToCell())]);
    }

    private void CheckGoal(List<StageProblem> problems)
    {
        if (Goal is not { } goal)
        {
            problems.Add(new("The stage has no goal. Put one on a floor tile."));
        }
        else if (!InGrid(goal.X, goal.Y))
        {
            problems.Add(new($"The goal at ({goal.X}, {goal.Y}) is off the {Stage.Width}×{Stage.Height} grid."));
        }
        else if (this[goal.X, goal.Y].Kind != CellKind.Floor)
        {
            problems.Add(new($"The goal is on {Name(this[goal.X, goal.Y])}. It needs a floor tile.", goal.X, goal.Y));
        }
    }

    private void CheckCharacters(List<StageProblem> problems)
    {
        if (Characters.Count == 0)
        {
            problems.Add(new("The stage has no character. Place one to four."));
        }
        else if (Characters.Count > MaxCharacters)
        {
            problems.Add(new($"The stage has {Characters.Count} characters. It can have at most four."));
        }

        for (var i = 0; i < Characters.Count; i++)
        {
            var (x, y) = Characters[i];
            var first = Characters.IndexOf((x, y));
            if (!InGrid(x, y))
            {
                problems.Add(new($"Character {i + 1} at ({x}, {y}) is off the {Stage.Width}×{Stage.Height} grid."));
            }
            else if (this[x, y].Kind != CellKind.Floor)
            {
                problems.Add(new($"Character {i + 1} starts on {Name(this[x, y])}. Characters need a floor tile.", x, y));
            }
            else if (Goal == (x, y))
            {
                problems.Add(new($"Character {i + 1} starts on the goal.", x, y));
            }
            else if (first < i)
            {
                problems.Add(new($"Character {i + 1} starts on the same cell as character {first + 1}.", x, y));
            }
        }
    }

    /// <summary>Reports tiles that are wrong on their own, and returns the cells of every well-formed group.</summary>
    private Dictionary<int, List<(int X, int Y)>> CheckTiles(List<StageProblem> problems)
    {
        var groups = new Dictionary<int, List<(int X, int Y)>>();
        for (var y = 0; y < Stage.Height; y++)
        {
            for (var x = 0; x < Stage.Width; x++)
            {
                var tile = this[x, y];
                if (!Enum.IsDefined(tile.Kind))
                {
                    problems.Add(new($"This cell holds an unknown kind of tile ({(int)tile.Kind}).", x, y));
                }
                else if (!tile.IsPart)
                {
                    if (tile.Group != 0)
                    {
                        problems.Add(new($"This {Kind(tile)} has group {tile.Group}, but only blocks and swivel parts belong to a group.", x, y));
                    }

                    if (tile.OverHole)
                    {
                        problems.Add(new($"This {Kind(tile)} is marked as over a hole, but only blocks and swivel parts rest on a hole. Use a hole tile instead.", x, y));
                    }
                }
                else if (tile.Group == 0)
                {
                    problems.Add(new($"This {Kind(tile)} has no group. Give it a group from 1 to {Tile.MaxGroup}.", x, y));
                }
                else if (tile.Group is < 1 or > Tile.MaxGroup)
                {
                    problems.Add(new($"This {Kind(tile)} has group {tile.Group}. Groups run from 1 to {Tile.MaxGroup}.", x, y));
                }
                else
                {
                    groups.TryAdd(tile.Group, []);
                    groups[tile.Group].Add((x, y));
                }
            }
        }

        return groups;
    }

    // The original moves a group by id alone, but a block in pieces looks like several blocks that move together.
    private static void CheckBlock(List<StageProblem> problems, int group, List<(int X, int Y)> cells)
    {
        var reached = new HashSet<(int X, int Y)> { cells[0] };
        var queue = new Queue<(int X, int Y)>(reached);
        while (queue.TryDequeue(out var cell))
        {
            foreach (var next in new[] { (cell.X + 1, cell.Y), (cell.X - 1, cell.Y), (cell.X, cell.Y + 1), (cell.X, cell.Y - 1) })
            {
                if (cells.Contains(next) && reached.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        var apart = cells.FindIndex(c => !reached.Contains(c));
        if (apart >= 0)
        {
            problems.Add(new($"Block group {group} is split: this block isn't connected to ({cells[0].X}, {cells[0].Y}).", cells[apart].X, cells[apart].Y));
        }
    }

    private void CheckSwivel(List<StageProblem> problems, int group, List<(int X, int Y)> cells)
    {
        var centers = cells.FindAll(c => this[c.X, c.Y].Kind == CellKind.SwivelCenter);
        var arms = cells.FindAll(c => this[c.X, c.Y].Kind == CellKind.SwivelArm);
        if (centers.Count == 0)
        {
            problems.Add(new($"Swivel group {group} has no center. Its arms need one to turn around.", arms[0].X, arms[0].Y));
            return;
        }

        var center = centers[0];
        if (centers.Count > 1)
        {
            problems.Add(new($"Swivel group {group} has a second center; the first is at ({center.X}, {center.Y}). A swivel has exactly one.", centers[1].X, centers[1].Y));
            return;
        }

        if (arms.Count == 0)
        {
            problems.Add(new($"This center of swivel group {group} has no arms.", center.X, center.Y));
        }

        foreach (var arm in arms)
        {
            if (arm.X != center.X && arm.Y != center.Y)
            {
                problems.Add(new($"This arm of swivel group {group} isn't in line with its center at ({center.X}, {center.Y}).", arm.X, arm.Y));
                continue;
            }

            // An arm can be longer than one cell, as long as only its own swivel's arms lie between it and the center.
            var (dx, dy) = (Math.Sign(center.X - arm.X), Math.Sign(center.Y - arm.Y));
            for (var (x, y) = (arm.X + dx, arm.Y + dy); (x, y) != center; (x, y) = (x + dx, y + dy))
            {
                if (this[x, y] != new Tile(CellKind.SwivelArm, group, this[x, y].OverHole))
                {
                    problems.Add(new($"This arm of swivel group {group} isn't joined to its center at ({center.X}, {center.Y}): ({x}, {y}) is in between.", arm.X, arm.Y));
                    break;
                }
            }
        }
    }

    private static string Kind(Tile tile) => tile.Kind switch
    {
        CellKind.Floor => "floor",
        CellKind.Wall => "wall",
        CellKind.Hole => "hole",
        CellKind.Block => "block",
        CellKind.SwivelCenter => "swivel center",
        CellKind.SwivelArm => "swivel arm",
        _ => "unknown tile",
    };

    private static string Name(Tile tile) => tile.Kind == CellKind.Floor ? "floor" : $"a {Kind(tile)}";

    private static bool InGrid(int x, int y) => x is >= 0 and < Stage.Width && y is >= 0 and < Stage.Height;

    private static int Index(int x, int y) => InGrid(x, y)
        ? (y * Stage.Width) + x
        : throw new ArgumentOutOfRangeException(nameof(x), $"({x}, {y}) is off the {Stage.Width}×{Stage.Height} grid.");
}
