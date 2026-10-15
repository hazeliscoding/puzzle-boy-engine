namespace PuzzleBoy.Rendering.Tests;

public sealed class DialogueTests
{
    [Fact]
    public void Leading_line_breaks_are_dropped_and_lines_split_with_tags_carried_across()
    {
        var lines = Dialogue.Prepare("<COLOR 0>\n\nOne <COLOR 1>two\nthree");

        Assert.Equal(["One two", "three"], lines.Select(l => new string([.. l.Select(r => r.Char)])));
        Assert.Equal(1, lines[1][0].Material);
        Assert.Equal(0, lines[0][0].Material);
    }

    [Fact]
    public void A_page_types_a_character_a_tick_and_shows_the_icon_a_tick_after()
    {
        var dialogue = new Dialogue(["ab\nc"]);
        var shown = new List<(int, bool)>();
        for (var tick = 0; tick < 5; tick++)
        {
            dialogue.Tick();
            shown.Add((dialogue.Revealed, dialogue.ShowsIcon));
        }

        Assert.Equal([(1, false), (2, false), (3, false), (3, true), (3, true)], shown);
    }

    [Fact]
    public void Confirm_finishes_a_typing_page_then_turns_it()
    {
        var dialogue = new Dialogue(["abc", "d"]);
        dialogue.Tick();

        dialogue.Confirm();
        Assert.Equal(3, dialogue.Revealed);
        Assert.Equal(0, dialogue.Page);

        dialogue.Tick();
        dialogue.Confirm();
        Assert.Equal((1, 0), (dialogue.Page, dialogue.Revealed));
        dialogue.Tick();
        Assert.Equal(1, dialogue.Revealed);
    }

    [Fact]
    public void After_the_last_page_the_name_fades_in_2_ticks_and_the_window_in_7()
    {
        var dialogue = new Dialogue(["a"]);
        dialogue.Tick();
        dialogue.Tick();
        dialogue.Confirm();

        dialogue.Tick();
        Assert.Equal(0.5f, dialogue.NameOpacity);
        Assert.False(dialogue.ShowsIcon);
        for (var tick = 1; tick < 7; tick++)
        {
            Assert.False(dialogue.Finished);
            dialogue.Tick();
        }

        Assert.Equal(0f, dialogue.WindowOpacity);
        Assert.True(dialogue.Finished);
    }
}
