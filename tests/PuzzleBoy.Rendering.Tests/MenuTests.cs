namespace PuzzleBoy.Rendering.Tests;

public sealed class MenuTests
{
    [Fact]
    public void The_menu_cursor_wraps_at_both_ends()
    {
        var menu = new Menu();

        menu.Move(-1);
        Assert.Equal(MenuItem.Quit, menu.Cursor);
        menu.Move(1);
        Assert.Equal(MenuItem.Undo, menu.Cursor);
    }

    [Fact]
    public void A_held_key_fires_first_then_every_second_tick_after_7()
    {
        var repeat = new KeyRepeat();
        var fired = Enumerable.Range(1, 12).Where(_ => repeat.Fires(down: true)).ToList();

        Assert.Equal([1, 8, 10, 12], fired);
        Assert.False(repeat.Fires(down: false));
        Assert.True(repeat.Fires(down: true));
    }

    [Fact]
    public void A_question_shows_its_choices_when_typed_and_starts_on_the_last()
    {
        var question = new Dialogue(["Quit?"], ["Yes", "No"]);
        for (var tick = 0; tick < 6; tick++)
        {
            question.Tick();
        }

        Assert.True(question.ShowsChoices);
        Assert.False(question.ShowsIcon);
        Assert.Equal(1, question.Choice);

        question.MoveChoice(1);
        Assert.Equal(0, question.Choice);
        question.Confirm();
        Assert.False(question.ShowsChoices);
    }

    [Fact]
    public void Cancelling_a_question_picks_the_last_option()
    {
        var question = new Dialogue(["Quit?"], ["Yes", "No"]);
        for (var tick = 0; tick < 6; tick++)
        {
            question.Tick();
        }

        question.MoveChoice(1);
        question.Cancel();

        Assert.Equal(1, question.Choice);
    }
}
