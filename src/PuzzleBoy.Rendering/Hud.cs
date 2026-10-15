using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PuzzleBoy.Nocturne;

namespace PuzzleBoy.Rendering;

/// <summary>The original's 1920x1080 UI layout scaled uniformly into the window and centred (re/m3-ui.md, section 1).</summary>
public static class UiLayout
{
    /// <summary>The layout's scale and the offset that centres it in a window of <paramref name="width"/> by <paramref name="height"/> pixels.</summary>
    public static (float Scale, Vector2 Offset) Fit(int width, int height)
    {
        var scale = Math.Min(width / 1920f, height / 1080f);
        return (scale, new Vector2((width - (1920 * scale)) / 2, (height - (1080 * scale)) / 2));
    }
}

/// <summary>Which controls the bar shows: the original's guide ids PZL01 (play) and PZL02 (scroll mode).</summary>
public enum BarSet
{
    /// <summary>The bar is off, as during dialogue.</summary>
    None,

    /// <summary>PZL01: Move, Rotate, Menu, Toggle Scroll, Angle.</summary>
    Play,

    /// <summary>PZL02: Scroll, Rotate, Toggle Scroll, Angle.</summary>
    Scroll,

    /// <summary>PZL03, while the menu is open: Select, Cancel, Confirm.</summary>
    Menu,

    /// <summary>PZL04, while help is open: Cancel.</summary>
    Help,
}

/// <summary>What the HUD shows this tick.</summary>
/// <param name="Stage">The stage number, 1 to 20.</param>
/// <param name="Clear">True while COMPLETE! shows.</param>
/// <param name="Undo">True while UNDO shows.</param>
/// <param name="Arrows">The scroll arrows to show, one bit per screen direction: 1 up, 2 right, 4 down, 8 left.</param>
/// <param name="ScrollTicks">Ticks since scroll mode began, for the arrows' bob.</param>
/// <param name="Bar">Which controls the bar shows.</param>
/// <param name="BarTicks">Ticks since the bar came on, for its fade-in.</param>
public readonly record struct HudState(int Stage, bool Clear, bool Undo, int Arrows, int ScrollTicks, BarSet Bar, int BarTicks);

/// <summary>The UI sheets the HUD and menus draw from, read from the player's install.</summary>
/// <param name="Sheet"><c>pzlUI01</c> from <c>common</c>: plates, glows, arrows, the menu and help.</param>
/// <param name="Words"><c>pzlUI_font01_en</c> from <c>ui_tex_en</c>: STAGE, COMPLETE! and UNDO.</param>
/// <param name="Digits"><c>pzlUI_font01_ja</c> from <c>common</c>, whose stage digits the original draws.</param>
/// <param name="Keyboard"><c>buttonUI01_en_keyboard</c> from <c>ui_tex_keyboard_en</c>: key icons and the bar's base strip.</param>
/// <param name="Talk"><c>talkUI01</c> from <c>common</c>: the dialogue window, name plate and choice box.</param>
/// <param name="Buttons"><c>buttonUI01_en_common</c> from <c>ui_tex_en</c>: the dialogue's advance icon.</param>
public sealed record UiSheets(TextureData Sheet, TextureData Words, TextureData Digits, TextureData Keyboard, TextureData Talk, TextureData Buttons);

/// <summary>
/// The stage counter, COMPLETE!, UNDO and the scroll arrows, at the original's layout (re/m3-ui.md, section 4).
/// Sprite rects are in sheet texels from the bottom-left, as Unity stores them; the sheets keep Unity's row order on the
/// GPU, so each sprite is drawn flipped.
/// </summary>
public sealed class Hud : IDisposable
{
    // The scroll arrows: rest centre, sheet rect on pzlUI01, and the way each bobs toward the centre (layout y is down).
    private static readonly (Vector2 Centre, Rectangle Sprite, Vector2 Inward)[] ScrollArrows =
    [
        (new(960, 84), new(1152, 808, 72, 72), new(0, 1)),
        (new(1774, 518), new(1380, 808, 72, 72), new(-1, 0)),
        (new(960, 952), new(1228, 808, 72, 72), new(0, -1)),
        (new(146, 518), new(1304, 808, 72, 72), new(1, 0)),
    ];

    // Key icons on the keyboard sheet (48x48). The original's PC keys: WASD move, Q/E turn, F menu, Tab scroll, V angle.
    private static readonly Rectangle KeyW = new(1612, 60, 48, 48), KeyA = new(28, 60, 48, 48), KeyS = new(1324, 60, 48, 48), KeyD = new(244, 60, 48, 48);
    private static readonly Rectangle KeyQ = new(1180, 60, 48, 48), KeyE = new(316, 60, 48, 48), KeyF = new(388, 60, 48, 48), KeyV = new(1540, 60, 48, 48);
    private static readonly Rectangle KeyTab = new(460, 108, 48, 48), KeySpace = new(532, 108, 48, 48), KeyBackspace = new(316, 108, 48, 48);

    // In the menu and help, Space (or Enter) confirms and Backspace cancels; the original's keys for these are unknown.
    private static readonly (Rectangle[] Keys, string Label)[] MenuSets =
        [([KeyW, KeyS], "GUIDE_L0001"), ([KeyBackspace], "GUIDE_L0002"), ([KeySpace], "GUIDE_L0003")];

    private static readonly (Rectangle[] Keys, string Label)[] HelpSets = [([KeyBackspace], "GUIDE_L0002")];

    // Help's text: string id, material, box (x, width) for centring, pen x, baseline (re/m3-ui.md, section 7).
    private static readonly (string Id, string Material, bool Centre, float X, float Width, float Baseline)[] HelpLines =
    [
        ("PBHELP_L0034", "TMC12", true, 203, 210.01f, 205.96f),
        ("PBHELP_L0035", "TMC24", false, 258, 0, 265.96f),
        ("PBHELP_L0036", "TMC24", false, 258, 0, 315.96f),
        ("PBHELP_L0037", "TMC12", true, 182, 252.01f, 395.96f),
        ("PBHELP_L0038", "TMC25", false, 238, 0, 456.96f),
        ("PBHELP_L0039", "TMC24", false, 258, 0, 505.96f),
        ("PBHELP_L0040", "TMC24", false, 258, 0, 555.96f),
        ("PBHELP_L0041", "TMC25", false, 238, 0, 638.96f),
        ("PBHELP_L0042", "TMC24", false, 258, 0, 687.96f),
        ("PBHELP_L0043", "TMC25", false, 238, 0, 770.96f),
        ("PBHELP_L0044", "TMC24", false, 258, 0, 819.96f),
        ("PBHELP_L0045", "TMC24", false, 258, 0, 869.96f),
    ];

    // The bar's sets, left to right: key icons and the label's string id.
    private static readonly (Rectangle[] Keys, string Label)[] PlaySets =
        [([KeyW, KeyA, KeyS, KeyD], "GUIDE_L0012"), ([KeyQ, KeyE], "GUIDE_L0021"), ([KeyF], "GUIDE_L0028"), ([KeyTab], "GUIDE_L0029"), ([KeyV], "GUIDE_L0032")];

    private static readonly (Rectangle[] Keys, string Label)[] ScrollSets =
        [([KeyW, KeyA, KeyS, KeyD], "GUIDE_L0016"), ([KeyQ, KeyE], "GUIDE_L0021"), ([KeyTab], "GUIDE_L0029"), ([KeyV], "GUIDE_L0032")];

    private readonly GraphicsDevice _device;
    private readonly SpriteBatch _sprites;
    private readonly Texture2D _sheet;
    private readonly Texture2D _words;
    private readonly Texture2D _digits;
    private readonly Texture2D _keyboard;
    private readonly Texture2D _talk;
    private readonly Texture2D _buttons;
    private readonly TextData _text;
    private readonly Dictionary<string, (Texture2D? Texture, Point At)> _baked = [];
    private readonly Dictionary<string, (Texture2D? Texture, Point At)> _typed = [];
    private (Dialogue Dialogue, int Page)? _typedPage;
    private (float Scale, Vector2 Offset) _fit;

    /// <summary>Uploads the sheets; text is baked on first use.</summary>
    public Hud(GraphicsDevice device, UiSheets sheets, TextData text)
    {
        _device = device;
        _sprites = new SpriteBatch(device);
        _sheet = BoardView.Upload(device, sheets.Sheet);
        _words = BoardView.Upload(device, sheets.Words);
        _digits = BoardView.Upload(device, sheets.Digits);
        _keyboard = BoardView.Upload(device, sheets.Keyboard);
        _talk = BoardView.Upload(device, sheets.Talk);
        _buttons = BoardView.Upload(device, sheets.Buttons);
        _text = text;
    }

    /// <summary>
    /// The dialogue window over the board and HUD (re/m3-ui.md, 5.2): the band, the name plate and name, the typed
    /// lines and, when a page is done, the advance icon.
    /// </summary>
    public void DrawDialogue(Dialogue dialogue)
    {
        // The body bakes again as each character appears; those bakes only live as long as their page.
        if (_typedPage != (dialogue, dialogue.Page))
        {
            Clear(_typed);
            _typedPage = (dialogue, dialogue.Page);
        }

        var name = _text.Strings[Dialogue.Speaker];
        Begin(BlendState.NonPremultiplied);
        Sliced(_talk, new(4, 4, 32, 352), 8, 8, new(-4, 732, 1928, 352), 0.702f * dialogue.WindowOpacity);
        Sliced(_talk, new(40, 276, 548, 98), 245, 245, new(183, 755, TextRenderer.Advance(_text.Font, name, 53) + 160, 98), 0.498f * dialogue.NameOpacity);
        Swap(BlendState.AlphaBlend);
        Text(name, 53, "MsgFont3", 263, 824.1f, dialogue.NameOpacity);

        // Colour tag n picks MsgFont n; 0 is the talk window's own material, which has MsgFont0's colours.
        string[] materials = ["FOT-MatisseProN-B SDF Material", "MsgFont1", "MsgFont2", "MsgFont3", "MsgFont4", "MsgFont5"];
        var left = dialogue.Revealed;
        for (var i = 0; i < dialogue.Lines.Count && left > 0; i++)
        {
            var shown = dialogue.Lines[i].Take(left).ToList();
            left -= shown.Count;
            Runs(_typed, shown, materials, 53, 213, 890.1f + (60 * i), dialogue.WindowOpacity);
        }

        if (dialogue.ShowsChoices)
        {
            DrawChoices(dialogue.Choices!, dialogue.Choice);
        }

        if (dialogue.ShowsIcon)
        {
            // shortcut: the icon's glow is steady; the original's 3-second talk_ok loop isn't decoded.
            Swap(BlendState.NonPremultiplied);
            Sprite(_buttons, new(1944, 152, 100, 100), new(1683, 935, 100, 100));
            Swap(BlendState.Additive);
            Sprite(_talk, new(652, 112, 100, 100), new(1683, 935, 100, 100));
            Sprite(_talk, new(548, 112, 100, 100), new(1683, 935, 100, 100));
        }

        _sprites.End();
    }

    /// <summary>The bar's fade-in: the original's smoothstep 12t^2 - 16t^3 over half a second, t in seconds.</summary>
    public static float BarOpacity(int ticks)
    {
        var t = Math.Min(ticks / 30f, 0.5f);
        return (12 * t * t) - (16 * t * t * t);
    }

    /// <summary>
    /// Lays out the bar's sets from the right: the last label ends at x <paramref name="right"/>, each set is its icons
    /// then its label, and <paramref name="gap"/> units separate sets. Returns each set's left edge, in set order.
    /// </summary>
    public static float[] BarLayout(IReadOnlyList<(int Icons, float Advance)> sets, float right = 1828, float gap = 36, float icon = 48)
    {
        var lefts = new float[sets.Count];
        for (var i = sets.Count - 1; i >= 0; i--)
        {
            lefts[i] = right - sets[i].Advance - (icon * sets[i].Icons);
            right = lefts[i] - gap;
        }

        return lefts;
    }

    /// <summary>The stage number as the counter's two digits: the tens are blank below 10.</summary>
    public static (int? Tens, int Ones) Digits(int stage) => (stage >= 10 ? stage / 10 : null, stage % 10);

    /// <summary>
    /// The scroll arrows' bob at <paramref name="seconds"/> into its half-second cycle: how far toward the centre, in
    /// layout units, and the glow's alpha. Both follow the original's three-key Hermite curves.
    /// </summary>
    public static (float Offset, float Glow) Bob(float seconds)
    {
        seconds %= 0.5f;
        return seconds < 0.25f
            ? (Hermite(0, 0, 24, 96, seconds / 0.25f, 0.25f), Hermite(0, 0, 0.5f, 2, seconds / 0.25f, 0.25f))
            : (Hermite(24, -96, 0, 0, (seconds - 0.25f) / 0.25f, 0.25f), Hermite(0.5f, -2, 0, 0, (seconds - 0.25f) / 0.25f, 0.25f));
    }

    /// <summary>Draws the HUD over the scene.</summary>
    public void Draw(HudState state)
    {
        _fit = UiLayout.Fit(_device.Viewport.Width, _device.Viewport.Height);

        // The stage counter: plate, glow, the word STAGE, then the digits.
        Begin(BlendState.NonPremultiplied);
        Sprite(_sheet, new(772, 1096, 372, 68), new(1494, 56, 372, 68));
        Swap(BlendState.Additive);
        Sprite(_sheet, new(632, 1096, 136, 124), new(1712, 30, 136, 124));
        Swap(BlendState.NonPremultiplied);
        Sprite(_words, new(1120, 144, 208, 44), new(1528, 68, 208, 44));
        var (tens, ones) = Digits(state.Stage);
        if (tens is { } t)
        {
            Sprite(_digits, Digit(t), new(1736, 60, 48, 60));
        }

        Sprite(_digits, Digit(ones), new(1774, 60, 48, 60));

        if (state.Undo)
        {
            Sprite(_words, new(1120, 68, 448, 72), new(736, 552, 448, 72));
        }

        // The arrows: a half-transparent base, then an animated additive glow.
        var (offset, glow) = Bob(state.ScrollTicks / 30f);
        for (var i = 0; i < ScrollArrows.Length; i++)
        {
            if ((state.Arrows & (1 << i)) != 0)
            {
                var (centre, sprite, inward) = ScrollArrows[i];
                Sprite(_sheet, sprite, Square(centre + (inward * offset)), 0.502f);
            }
        }

        Swap(BlendState.Additive);
        for (var i = 0; i < ScrollArrows.Length; i++)
        {
            if ((state.Arrows & (1 << i)) != 0)
            {
                var (centre, sprite, inward) = ScrollArrows[i];
                Sprite(_sheet, sprite, Square(centre + (inward * offset)), glow);
            }
        }

        if (state.Clear)
        {
            Swap(BlendState.NonPremultiplied);
            Sprite(_words, new(4, 4, 1112, 224), new(424, 444, 1112, 224));
            Swap(BlendState.Additive);
            Sprite(_words, new(4, 232, 1112, 224), new(424, 444, 1112, 224));
        }

        _sprites.End();
        var sets = state.Bar switch
        {
            BarSet.Play => PlaySets,
            BarSet.Scroll => ScrollSets,
            BarSet.Menu => MenuSets,
            BarSet.Help => HelpSets,
            _ => null,
        };
        if (sets is not null)
        {
            DrawBar(sets, BarOpacity(state.BarTicks));
        }
    }

    /// <summary>The □ menu (re/m3-ui.md, section 6): its panel, the cursor and the four items.</summary>
    public void DrawMenu(Menu menu)
    {
        Begin(BlendState.NonPremultiplied);
        Sprite(_sheet, new(632, 808, 516, 284), new(90, 138, 516, 284));
        Sprite(_sheet, new(1964, 4, 60, 56), new(104, 164 + (58 * (int)menu.Cursor), 60, 56));
        Swap(BlendState.AlphaBlend);
        for (var i = 0; i < Menu.Labels.Length; i++)
        {
            Text(_text.Strings[Menu.Labels[i]], 42, "TMC22", 168, 207.96f + (58 * i));
        }

        _sprites.End();
    }

    /// <summary>The rules screen (re/m3-ui.md, section 7): glows, the panel with its pictures, header icons, then the text.</summary>
    public void DrawHelp()
    {
        // shortcut: the top glow is the bottom one turned 180 degrees; its borders are equal, so mirroring the ends doesn't matter.
        Begin(BlendState.Additive);
        Sliced(_sheet, new(1936, 4, 24, 128), 8, 8, new(-4, 932, 1928, 128), 1);
        Sliced(_sheet, new(1936, 4, 24, 128), 8, 8, new(-4, 20, 1928, 128), 1, upsideDown: true);
        Swap(BlendState.NonPremultiplied);
        Sprite(_sheet, new(4, 4, 1928, 800), new(-4, 140, 1928, 800));
        foreach (var (x, y) in new[] { (98f, 160f), (458.01f, 160f), (98f, 350f), (458.01f, 350f) })
        {
            Sprite(_sheet, new(1964, 64, 60, 60), new(x, y, 60, 60));
        }

        Swap(BlendState.AlphaBlend);
        foreach (var (id, material, centre, x, width, baseline) in HelpLines)
        {
            var text = _text.Strings[id];
            var pen = centre ? x + ((width - TextRenderer.Advance(_text.Font, text, 42)) / 2) : x;
            Text(text, 42, material, pen, baseline);
        }

        _sprites.End();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _sprites.Dispose();
        _sheet.Dispose();
        _words.Dispose();
        _digits.Dispose();
        _keyboard.Dispose();
        _talk.Dispose();
        _buttons.Dispose();
        Clear(_baked);
        Clear(_typed);
    }

    private static void Clear(Dictionary<string, (Texture2D? Texture, Point At)> bakes)
    {
        foreach (var (texture, _) in bakes.Values)
        {
            texture?.Dispose();
        }

        bakes.Clear();
    }

    /// <summary>
    /// A question's choice box, its bottom-right corner at (1868, 758), with the cursor on <paramref name="choice"/>.
    /// Expects an open batch with <see cref="BlendState.AlphaBlend"/> and leaves one open.
    /// </summary>
    private void DrawChoices(IReadOnlyList<string> choices, int choice)
    {
        // shortcut: no footage shows this box, so its rows are spaced evenly in the window's native height. Upgrade
        // with the talk01_choice prefab's layout if a capture of the quit question turns up.
        var widest = choices.Max(c => TextRenderer.Advance(_text.Font, c, 53));
        var (width, height) = (Math.Max(480, 48 + 52 + widest + 48), 244f);
        var (x, y) = (1868 - width, 758 - height);
        Swap(BlendState.NonPremultiplied);
        Sliced(_talk, new(40, 28, 504, 244), 240, 240, new(x, y, width, height), 0.702f);
        var rows = choices.Select((_, i) => y + (height / (choices.Count + 1) * (i + 1))).ToArray();
        Sprite(_talk, new(596, 214, 40, 34), new(x + 48, rows[choice] - 17, 40, 34));
        Swap(BlendState.Additive);
        Sprite(_talk, new(596, 214, 40, 34), new(x + 48, rows[choice] - 17, 40, 34), 0.196f);
        Sprite(_talk, new(596, 214, 40, 34), new(x + 48, rows[choice] - 17, 40, 34), 0.196f);
        Swap(BlendState.AlphaBlend);
        for (var i = 0; i < choices.Count; i++)
        {
            Text(choices[i], 53, "FOT-MatisseProN-B SDF Material", x + 48 + 52, TextRenderer.MiddleBaseline(_text.Font, rows[i], 53));
        }
    }

    /// <summary>The controls bar: a dark strip, then each set's key icons and label, right to left.</summary>
    private void DrawBar((Rectangle[] Keys, string Label)[] sets, float opacity)
    {
        var labels = sets.Select(s => _text.Strings[s.Label]).ToArray();
        var lefts = BarLayout([.. sets.Select((s, i) => (s.Keys.Length, TextRenderer.Advance(_text.Font, labels[i], 32)))]);
        Begin(BlendState.NonPremultiplied);
        Sprite(_keyboard, new(4, 4, 1508, 52), new(416, 980, 1508, 52), opacity);
        for (var i = 0; i < sets.Length; i++)
        {
            for (var k = 0; k < sets[i].Keys.Length; k++)
            {
                Sprite(_keyboard, sets[i].Keys[k], new(lefts[i] + (48 * k), 982, 48, 48), opacity);
            }
        }

        Swap(BlendState.AlphaBlend);
        for (var i = 0; i < sets.Length; i++)
        {
            Text(labels[i], 32, "FOT-MatisseProN-B SDF Material", lefts[i] + (48 * sets[i].Keys.Length), 1019.16f, opacity);
        }

        _sprites.End();
    }

    /// <summary>
    /// Draws text baked by <see cref="TextRenderer"/> at a layout pen and baseline, faded by <paramref name="opacity"/>,
    /// inside an open sprite batch using <see cref="BlendState.AlphaBlend"/>. Bakes are cached; the window doesn't resize.
    /// </summary>
    private void Text(string text, float size, string material, float penX, float baseline, float opacity = 1) =>
        Runs(_baked, [.. text.Select(c => (c, 0))], [material], size, penX, baseline, opacity);

    /// <summary>Draws characters with per-character materials (indices into <paramref name="materials"/>), baking into <paramref name="cache"/>.</summary>
    private void Runs(Dictionary<string, (Texture2D? Texture, Point At)> cache, List<(char Char, int Material)> runs, string[] materials,
        float size, float penX, float baseline, float opacity)
    {
        var key = $"{string.Concat(runs.Select(r => $"{r.Char}{r.Material}"))}|{string.Join(',', materials)}|{size}|{penX}|{baseline}";
        if (!cache.TryGetValue(key, out var baked))
        {
            var image = TextRenderer.Bake(_text.Font, [.. materials.Select(m => _text.Materials[m])], runs, size, penX, baseline, _fit);
            Texture2D? texture = null;
            if (image.Width > 0)
            {
                texture = new Texture2D(_device, image.Width, image.Height);
                texture.SetData(image.Rgba);
            }

            cache[key] = baked = (texture, new Point(image.X, image.Y));
        }

        if (baked.Texture is not null)
        {
            // Baked text is premultiplied, so fading it scales all four channels.
            _sprites.Draw(baked.Texture, baked.At.ToVector2(), Color.White * opacity);
        }
    }

    /// <summary>
    /// A sprite cut in three across: fixed borders of <paramref name="left"/> and <paramref name="right"/> texels at each
    /// end and a stretched middle. A target narrower than both borders shrinks them in proportion, as Unity does.
    /// </summary>
    private void Sliced(Texture2D sheet, Rectangle unity, int left, int right, RectangleF layout, float alpha, bool upsideDown = false)
    {
        var shrink = Math.Min(1, layout.Width / (left + right));
        var (l, r) = (left * shrink, right * shrink);
        Sprite(sheet, new(unity.X, unity.Y, left, unity.Height), new(layout.X, layout.Y, l, layout.Height), alpha, upsideDown);
        if (layout.Width > l + r)
        {
            Sprite(sheet, new(unity.X + left, unity.Y, unity.Width - left - right, unity.Height), new(layout.X + l, layout.Y, layout.Width - l - r, layout.Height), alpha, upsideDown);
        }

        Sprite(sheet, new(unity.Right - right, unity.Y, right, unity.Height), new(layout.X + layout.Width - r, layout.Y, r, layout.Height), alpha, upsideDown);
    }

    /// <summary>A cubic Hermite segment from p0 (slope m0) to p1 (slope m1) over <paramref name="length"/> seconds, at fraction <paramref name="s"/>.</summary>
    private static float Hermite(float p0, float m0, float p1, float m1, float s, float length)
    {
        var (s2, s3) = (s * s, s * s * s);
        return (((2 * s3) - (3 * s2) + 1) * p0) + ((s3 - (2 * s2) + s) * length * m0) + (((-2 * s3) + (3 * s2)) * p1) + ((s3 - s2) * length * m1);
    }

    /// <summary>Digit d's 48x60 sprite on the digit sheet: 1 to 9 run from x 1120, and 0 sits at x 1552.</summary>
    private static Rectangle Digit(int d) => new(d == 0 ? 1552 : 1120 + (48 * (d - 1)), 4, 48, 60);

    private static RectangleF Square(Vector2 centre) => new(centre.X - 36, centre.Y - 36, 72, 72);

    private void Begin(BlendState blend) =>
        _sprites.Begin(SpriteSortMode.Deferred, blend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

    private void Swap(BlendState blend)
    {
        _sprites.End();
        Begin(blend);
    }

    /// <summary>Stretches a sheet rect (Unity's, from the bottom-left) over a layout rect, at <paramref name="alpha"/>.</summary>
    private void Sprite(Texture2D sheet, Rectangle unity, RectangleF layout, float alpha = 1, bool upsideDown = false)
    {
        var position = _fit.Offset + (new Vector2(layout.X, layout.Y) * _fit.Scale);
        var scale = new Vector2(layout.Width / unity.Width, layout.Height / unity.Height) * _fit.Scale;

        // Fading a straight-alpha sprite scales its alpha only; the additive glows take the same alpha as their strength.
        // The sheets keep Unity's bottom-up rows, so an upright sprite is flipped vertically; turned 180 degrees, horizontally.
        var flip = upsideDown ? SpriteEffects.FlipHorizontally : SpriteEffects.FlipVertically;
        _sprites.Draw(sheet, position, unity, new Color(Color.White, alpha), 0, Vector2.Zero, scale, flip, 0);
    }

    /// <summary>A rectangle in layout units, which can be fractional.</summary>
    private readonly record struct RectangleF(float X, float Y, float Width, float Height);
}
