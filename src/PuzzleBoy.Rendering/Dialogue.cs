namespace PuzzleBoy.Rendering;

/// <summary>
/// The Manikin Kid's pages at a stage start, timed as in the HD footage (re/m3-ui.md, section 5): one character a tick,
/// the advance icon a tick after the last one, confirm to turn the page, then a short fade.
/// </summary>
/// <param name="pages">The pages' text, with the original's <c>&lt;COLOR n&gt;</c> tags and line breaks.</param>
/// <param name="choices">
/// For a question, its options: they show once the last page has typed, with no advance icon, and the cursor starts on
/// the last one. Null for plain talk.
/// </param>
public sealed class Dialogue(IEnumerable<string> pages, IReadOnlyList<string>? choices = null)
{
    // After the last page the name and its plate fade over 2 ticks, the text and the window over 7.
    private const int NameFade = 2;
    private const int WindowFade = 7;

    private readonly List<List<List<(char Char, int Material)>>> _pages = [.. pages.Select(Prepare)];
    private int _revealed;
    private int _doneTicks;
    private int _closing = -1;

    /// <summary>The stages whose start the Manikin Kid talks over, and his pages' string ids (re/m3-ui.md, 5.1).</summary>
    public static IReadOnlyDictionary<int, string[]> Stages { get; } = new Dictionary<int, string[]>
    {
        [1] = ["PBMESSAGE_L0000", "PBMESSAGE_L0001", "PBMESSAGE_L0002"],
        [6] = ["PBMESSAGE_L0003"],
        [11] = ["PBMESSAGE_L0004"],
        [16] = ["PBMESSAGE_L0005"],
        [20] = ["PBMESSAGE_L0006"],
    };

    /// <summary>The speaker's name's string id, the same on every page.</summary>
    public const string Speaker = "pbMessage_pb_mes_01_000";

    /// <summary>Which page shows, counting from 0.</summary>
    public int Page { get; private set; }

    /// <summary>The current page's lines, each a run of characters with the material its colour tag chose.</summary>
    public IReadOnlyList<List<(char Char, int Material)>> Lines => _pages[Page];

    /// <summary>How many of the page's characters show so far; line breaks don't count.</summary>
    public int Revealed => _revealed;

    /// <summary>True from the tick after the page's last character until confirm; a question shows its choices instead.</summary>
    public bool ShowsIcon => _closing < 0 && _revealed >= Total && _doneTicks >= 1 && !ShowsChoices;

    /// <summary>The question's options, or null for plain talk.</summary>
    public IReadOnlyList<string>? Choices => choices;

    /// <summary>True once a question's last page has typed, until it's answered.</summary>
    public bool ShowsChoices => choices is not null && _closing < 0 && Page == _pages.Count - 1 && _revealed >= Total;

    /// <summary>The option under the cursor; the original starts it on the last option, "No".</summary>
    public int Choice { get; private set; } = (choices?.Count ?? 1) - 1;

    /// <summary>The window's and the text's opacity: 1, then fading over 7 ticks after the last page.</summary>
    public float WindowOpacity => _closing < 0 ? 1 : 1 - (Math.Min(_closing, WindowFade) / (float)WindowFade);

    /// <summary>The name's and its plate's opacity, which fade faster.</summary>
    public float NameOpacity => _closing < 0 ? 1 : 1 - (Math.Min(_closing, NameFade) / (float)NameFade);

    /// <summary>True once the fade has ended and play can go on.</summary>
    public bool Finished => _closing >= WindowFade;

    private int Total => _pages[Page].Sum(line => line.Count);

    /// <summary>
    /// A page's lines: leading line breaks (and the tags before them) dropped, split at line breaks, at most 3 lines.
    /// Colour tags carry across the split.
    /// </summary>
    public static List<List<(char Char, int Material)>> Prepare(string text)
    {
        var runs = TextRenderer.Runs(text);
        var lines = new List<List<(char, int)>> { new() };
        foreach (var run in runs.SkipWhile(r => r.Char == '\n'))
        {
            if (run.Char == '\n')
            {
                lines.Add([]);
            }
            else
            {
                lines[^1].Add(run);
            }
        }

        return [.. lines.Take(3)];
    }

    /// <summary>Advances one tick: types a character, counts toward the icon, or fades.</summary>
    public void Tick()
    {
        if (_closing >= 0)
        {
            _closing++;
        }
        else if (_revealed < Total)
        {
            _revealed++;
        }
        else
        {
            _doneTicks++;
        }
    }

    /// <summary>
    /// Confirm: finishes a page still typing (a port choice; the footage never shows it), turns a finished page, or
    /// starts the fade after the last one.
    /// </summary>
    public void Confirm()
    {
        if (_closing >= 0)
        {
            return;
        }

        if (_revealed < Total)
        {
            (_revealed, _doneTicks) = (Total, 0);
        }
        else if (ShowsChoices)
        {
            _closing = 0;
        }
        else if (ShowsIcon && Page + 1 < _pages.Count)
        {
            (Page, _revealed, _doneTicks) = (Page + 1, 0, 0);
        }
        else if (ShowsIcon)
        {
            _closing = 0;
        }
    }

    /// <summary>Moves a question's cursor <paramref name="by"/> options, wrapping. Does nothing when no choices show.</summary>
    public void MoveChoice(int by)
    {
        if (ShowsChoices)
        {
            Choice = (((Choice + by) % choices!.Count) + choices.Count) % choices.Count;
        }
    }

    /// <summary>Cancel on a question: picks the last option, "No", and closes.</summary>
    public void Cancel()
    {
        if (ShowsChoices)
        {
            Choice = choices!.Count - 1;
            _closing = 0;
        }
    }
}
