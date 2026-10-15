using PuzzleBoy.Rules;
using PuzzleBoy.Rules.Tests;

namespace PuzzleBoy.Rendering.Tests;

// The original's timing in ticks (puzzle-boy's ROADMAP.md, "M3 findings": moves, camera and controls).
public sealed class TimingTests
{
    [Fact]
    public void A_turn_of_the_view_takes_9_ticks_and_ends_a_quarter_turn_away()
    {
        var camera = new ViewCamera();

        Assert.True(camera.TurnRight());
        for (var tick = 1; tick < 9; tick++)
        {
            Assert.True(camera.IsBusy);
            camera.Tick();
        }

        Assert.False(camera.IsBusy);
        Assert.Equal(90, camera.Yaw);
    }

    [Fact]
    public void A_second_turn_or_an_angle_change_waits_for_the_turn_to_end()
    {
        var camera = new ViewCamera();
        camera.TurnLeft();

        Assert.False(camera.TurnLeft());
        Assert.False(camera.ToggleAngle());
        Assert.Equal(3, camera.Mode);
    }

    [Fact]
    public void After_an_angle_change_L1_and_R1_wait_3_ticks()
    {
        var camera = new ViewCamera();

        Assert.True(camera.ToggleAngle());
        camera.Tick();
        Assert.False(camera.TurnRight());
        camera.Tick();
        Assert.False(camera.TurnRight());
        camera.Tick();
        Assert.True(camera.TurnRight());
    }

    [Fact]
    public void A_turn_in_the_overhead_view_takes_12_ticks_and_still_ends_a_quarter_turn_away()
    {
        var camera = new ViewCamera();
        camera.ToggleAngle();
        for (var tick = 0; tick < 3; tick++)
        {
            camera.Tick();
        }

        Assert.True(camera.TurnRight());
        for (var tick = 1; tick < 12; tick++)
        {
            Assert.True(camera.IsTurning);
            camera.Tick();
        }

        Assert.False(camera.IsTurning);
        Assert.Equal(90, camera.Yaw);
    }

    [Theory]
    [InlineData("1..", 4)]
    [InlineData("1a.", 5)]
    [InlineData("1ao", 9)]
    public void The_next_input_waits_4_ticks_after_a_walk_5_after_a_push_and_9_after_a_fall(string drawing, int ticks)
    {
        var board = Ascii.Board(drawing);
        board.Step(Direction.PlusX);
        var animation = new MoveAnimation(board.LastMove!, (0, 0));

        for (var tick = 0; tick < ticks; tick++)
        {
            Assert.False(animation.Done);
            animation.Tick();
        }

        Assert.True(animation.Done);
    }

    [Fact]
    public void A_slide_shows_a_quarter_on_the_input_tick_and_is_whole_after_3_more()
    {
        var board = Ascii.Board("1..");
        board.Step(Direction.PlusX);
        var animation = new MoveAnimation(board.LastMove!, (0, 0));
        var slides = new List<float> { animation.Slide };
        for (var tick = 0; tick < 4; tick++)
        {
            animation.Tick();
            slides.Add(animation.Slide);
        }

        Assert.Equal([0.25f, 0.5f, 0.75f, 1f, 1f], slides);
    }

    [Fact]
    public void A_fall_sinks_over_the_4_ticks_after_the_push_and_ends_on_the_8th()
    {
        var board = Ascii.Board("1ao");
        board.Step(Direction.PlusX);
        var animation = new MoveAnimation(board.LastMove!, (0, 0));
        var sinks = new List<float>();
        for (var tick = 0; tick < 9; tick++)
        {
            sinks.Add(animation.Sink);
            Assert.Equal(tick == 8, animation.FallEnds);
            animation.Tick();
        }

        Assert.Equal([0f, 0f, 0f, 0f, 0.25f, 0.5f, 0.75f, 1f, 1f], sinks);
    }
    [Fact]
    public void A_fall_starts_on_the_tick_after_the_push_ends()
    {
        var board = Ascii.Board("1ao");
        board.Step(Direction.PlusX);
        var animation = new MoveAnimation(board.LastMove!, (0, 0));
        var starts = new List<int>();
        for (var tick = 0; tick < 9; tick++)
        {
            if (animation.FallStarts)
            {
                starts.Add(tick);
            }

            animation.Tick();
        }

        Assert.Equal([4], starts);
    }

    [Fact]
    public void Arriving_at_the_goal_is_signalled_on_the_last_tick_of_the_slide()
    {
        var board = Ascii.Board(".1.");
        board.Step(Direction.PlusX);
        var animation = new MoveAnimation(board.LastMove!, (1, 0));
        var arrivals = new List<int>();
        for (var tick = 0; tick < 4; tick++)
        {
            if (animation.ArrivesAt((2, 0)))
            {
                arrivals.Add(tick);
            }

            Assert.False(animation.ArrivesAt((0, 0)));
            animation.Tick();
        }

        Assert.Equal([3], arrivals);
    }
}
