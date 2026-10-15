namespace PuzzleBoy.Rules;

/// <summary>One thing wrong with an authored stage, worded for the player who made it.</summary>
/// <param name="Message">A full sentence. It names any other cell it mentions as (x, y), but not its own position.</param>
/// <param name="X">The column of the cell the problem is about, or null when it isn't about one cell.</param>
/// <param name="Y">The row of that cell; null exactly when <paramref name="X"/> is.</param>
public sealed record StageProblem(string Message, int? X = null, int? Y = null)
{
    /// <summary>The message, after the cell's position as <c>(x, y): </c> when it has one.</summary>
    public override string ToString() => X is { } x && Y is { } y ? $"({x}, {y}): {Message}" : Message;
}

/// <summary>
/// A stage file or authored stage that can't be played, with every problem found. It derives from
/// <see cref="FormatException"/> because <see cref="InvalidDataException"/>, which <see cref="Stage.Parse"/> throws, is sealed.
/// </summary>
public sealed class StageFormatException : FormatException
{
    /// <summary>Reports <paramref name="problems"/>, of which there is at least one.</summary>
    /// <exception cref="ArgumentException"><paramref name="problems"/> is empty.</exception>
    public StageFormatException(IReadOnlyList<StageProblem> problems)
        : base(Describe(problems))
    {
        Problems = problems;
    }

    /// <summary>Reports one problem that isn't about a particular cell.</summary>
    public StageFormatException(string message)
        : this([new StageProblem(message)])
    {
    }

    /// <summary>Every problem found, never empty.</summary>
    public IReadOnlyList<StageProblem> Problems { get; }

    private static string Describe(IReadOnlyList<StageProblem> problems) => problems.Count switch
    {
        0 => throw new ArgumentException("There must be at least one problem.", nameof(problems)),
        1 => problems[0].ToString(),
        _ => $"The stage has {problems.Count} problems:\n{string.Join('\n', problems)}",
    };
}
