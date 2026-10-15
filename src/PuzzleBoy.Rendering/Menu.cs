namespace PuzzleBoy.Rendering;

/// <summary>The □ menu's items, in the order they show.</summary>
public enum MenuItem
{
    /// <summary>Takes back the last push or turn; the menu stays open.</summary>
    Undo,

    /// <summary>Starts the stage again.</summary>
    Restart,

    /// <summary>Shows the rules.</summary>
    Help,

    /// <summary>Asks whether to stop playing.</summary>
    Quit,
}

/// <summary>The □ menu's cursor (re/m3-ui.md, section 6): four items, wrapping at both ends.</summary>
public sealed class Menu
{
    /// <summary>The string ids of the items' labels, in <see cref="MenuItem"/> order.</summary>
    public static readonly string[] Labels = ["PBHELP_L0046", "PBHELP_L0047", "PBHELP_L0048", "PBHELP_L0049"];

    /// <summary>The item under the cursor.</summary>
    public MenuItem Cursor { get; private set; }

    /// <summary>Puts the cursor back on Undo, as opening the menu does.</summary>
    public void Reset() => Cursor = MenuItem.Undo;

    /// <summary>Moves the cursor <paramref name="by"/> items, wrapping around.</summary>
    public void Move(int by) => Cursor = (MenuItem)((((int)Cursor + by) % 4 + 4) % 4);
}

/// <summary>
/// The original pad layer's repeat for a held key (re/m3-input-camera.md, <c>dds3PadUpdate</c>): it fires on the first
/// tick, then on every second tick once the key has been held more than 7.
/// </summary>
public sealed class KeyRepeat
{
    private int _held;

    /// <summary>Counts one tick of the key being <paramref name="down"/>, and says whether it fires.</summary>
    public bool Fires(bool down)
    {
        _held = down ? _held + 1 : 0;
        return _held == 1 || (_held > 7 && _held % 2 == 0);
    }
}
