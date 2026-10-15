using PuzzleBoy.Rules;

namespace PuzzleBoy.Rendering;

/// <summary>
/// Plays one step with the original's timing (puzzle-boy's ROADMAP.md, "M3 findings": moves). The board has already changed;
/// this only says how far along the picture is. Age 0 is the tick that took the input.
/// </summary>
/// <param name="move">The step to play.</param>
/// <param name="from">Where the moving character started.</param>
public sealed class MoveAnimation(Move move, (int X, int Y) from)
{
    /// <summary>The step being played.</summary>
    public Move Move { get; } = move;

    /// <summary>Where the moving character started.</summary>
    public (int X, int Y) From { get; } = from;

    /// <summary>Ticks since the input tick.</summary>
    public int Age { get; private set; }

    /// <summary>How much of the 4-tick slide shows, 0.25 to 1. The original draws the first quarter on the input tick.</summary>
    public float Slide => Math.Min(Age + 1, 4) / 4f;

    /// <summary>How far a fallen block has sunk, 0 to 1 cell. It sinks on the 4 ticks after the push.</summary>
    public float Sink => Move.Fell ? Math.Clamp((Age - 3) / 4f, 0, 1) : 0;

    /// <summary>True while a fallen block is still visible, so the holes under it should still look open.</summary>
    public bool Falling => Move.Fell && Age < 8;

    /// <summary>True on the tick a fall ends, when the original fills the holes and shakes the camera.</summary>
    public bool FallEnds => Move.Fell && Age == 8;

    /// <summary>True on the tick a fall starts, right after the push: the original plays SE03 then.</summary>
    public bool FallStarts => Move.Fell && Age == 4;

    /// <summary>The cell the moving character ends on.</summary>
    public (int X, int Y) Destination
    {
        get
        {
            var (dx, dy) = Move.Direction.Offset();
            return (From.X + (dx * Move.Cells), From.Y + (dy * Move.Cells));
        }
    }

    /// <summary>True on the last tick of the slide, when the original checks for the goal and plays SE09 on arrival.</summary>
    public bool ArrivesAt((int X, int Y) goal) => Age == 3 && Destination == goal;

    /// <summary>True once the original would read the next input: 4 ticks after a walk, 5 after a push or turn, 9 after a fall.</summary>
    public bool Done => Age >= (Move.Fell ? 9 : Move.Result == StepResult.Walked ? 4 : 5);

    /// <summary>Advances one tick.</summary>
    public void Tick() => Age++;

    /// <summary>Where character <paramref name="index"/> is drawn, in cells: sliding from its start while its move plays.</summary>
    public static (float X, float Y) Where(Board board, MoveAnimation? animation, int index)
    {
        var c = board.Characters[index];
        if (animation is null || animation.Move.Mover != index)
        {
            return (c.X, c.Y);
        }

        var slide = animation.Slide;
        return (animation.From.X + ((c.X - animation.From.X) * slide), animation.From.Y + ((c.Y - animation.From.Y) * slide));
    }
}
