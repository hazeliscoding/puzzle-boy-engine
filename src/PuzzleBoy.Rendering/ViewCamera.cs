using Microsoft.Xna.Framework;
using PuzzleBoy.Rules;

namespace PuzzleBoy.Rendering;

/// <summary>
/// The original's follow camera (puzzle-boy's ROADMAP.md, "M3 findings": camera and controls), advanced one tick at a time.
/// Distances are in cells (the original's 40 units are 1 here), and grid +y runs toward -z, away from the default view.
/// </summary>
public sealed class ViewCamera
{
    // A turn steps 10° a tick for 9 ticks in the normal view and 0.14 rad (8.19°) for 12 in the overhead view,
    // snapping to the exact quarter turn on its last tick.
    private const float NormalTurnStep = 10f;
    private const int NormalTurnTicks = 9;
    private const float OverheadTurnStep = 8.19f;
    private const int OverheadTurnTicks = 12;

    // The angle change steps the pitch on the press tick and the next 2, and L1/R1 wait one tick more.
    private const int AngleSteps = 3;

    // The original's offsets from the character: (0, 400, -240) in the normal view and (0, 450, 0) overhead.
    private const float NormalBack = 6f;
    private const float NormalHeight = 10f;
    private const float OverheadHeight = 11.25f;

    private float _yaw;
    private float _targetYaw;
    private float _turnStep;
    private int _turnTicksLeft;
    private Vector3? _orbit;
    private bool _overhead;
    private int _angle = AngleSteps + 1;
    private int _quakeTicks;
    private float _quakeX;
    private float _quakeY;

    /// <summary>R1 presses minus L1 presses, mod 4. It turns the keys and the view together.</summary>
    public int Mode { get; private set; }

    /// <summary>True while an L1/R1 turn runs; the original refuses its □ menu until it ends.</summary>
    public bool IsTurning => _turnTicksLeft > 0;

    /// <summary>True while a turn or an angle change runs; the original ignores L1, R1 and ○ until it ends.</summary>
    public bool IsBusy => IsTurning || _angle <= AngleSteps;

    /// <summary>The view's yaw in degrees: 0 looks toward grid +y, and R1 adds 90 over a turn.</summary>
    public float Yaw => _yaw;

    /// <summary>
    /// The grid direction for a screen direction: 0 up, 1 right, 2 down, 3 left. Up is always away from the camera,
    /// because the original's key table turns with the view: direction = (key - mode) mod 4.
    /// </summary>
    public Direction DirectionFor(int screen) => (Direction)((screen - Mode + 4) % 4);

    /// <summary>L1: turns the view a quarter turn, over 9 ticks in the normal view or 12 overhead. False while busy.</summary>
    public bool TurnLeft() => Turn(-1);

    /// <summary>R1: the opposite of <see cref="TurnLeft"/>.</summary>
    public bool TurnRight() => Turn(1);

    /// <summary>○: switches between the normal and overhead views over 3 ticks. False while busy.</summary>
    public bool ToggleAngle()
    {
        if (IsBusy)
        {
            return false;
        }

        // The first of the 3 steps shows on the press tick.
        _overhead = !_overhead;
        _angle = 1;
        return true;
    }

    /// <summary>Starts the 30-tick shake a block's fall ends with.</summary>
    public void Quake()
    {
        // The original starts from (320, 800) x 0.01 units, here in cells.
        _quakeTicks = 30;
        _quakeX = 3.2f / 40f;
        _quakeY = 8f / 40f;
    }

    /// <summary>Advances the turn, angle change and shake by one tick.</summary>
    public void Tick()
    {
        if (_turnTicksLeft > 0)
        {
            _turnTicksLeft--;
            _yaw = _turnTicksLeft == 0 ? _targetYaw : _yaw + _turnStep;
        }

        if (_angle <= AngleSteps)
        {
            _angle++;
        }

        if (_quakeTicks > 0)
        {
            _quakeX -= _quakeX / (_quakeTicks + 5);
            _quakeY -= _quakeY / (_quakeTicks + 5);
            _quakeTicks--;
        }
    }

    /// <summary>The view matrix for a camera following <paramref name="pivot"/>, the point the original orbits.</summary>
    public Matrix View(Vector3 pivot)
    {
        // A turn in the normal view orbits the point where it started and doesn't follow the character; the camera
        // catches up when it ends. The overhead view keeps following.
        if (IsTurning && !_overhead)
        {
            _orbit ??= pivot;
            pivot = _orbit.Value;
        }
        else
        {
            _orbit = null;
        }

        // How far the view has gone toward overhead: pitch steps 60, 70, 80, 90 and the position moves in thirds.
        var toward = Math.Min(_angle, AngleSteps) / (float)AngleSteps;
        var overhead = _overhead ? toward : 1 - toward;
        var pitch = MathHelper.ToRadians(60 + (30 * overhead));
        var yaw = MathHelper.ToRadians(_yaw);
        var away = new Vector3(MathF.Sin(yaw), 0, MathF.Cos(yaw));
        var normal = (away * NormalBack) + (Vector3.Up * NormalHeight);
        var position = pivot + Vector3.Lerp(normal, Vector3.Up * OverheadHeight, overhead);
        var forward = (-away * MathF.Cos(pitch)) - (Vector3.Up * MathF.Sin(pitch));
        var up = (-away * MathF.Sin(pitch)) + (Vector3.Up * MathF.Cos(pitch));

        // INFERRED shape: the original's shake is a random walk whose float arguments the decompiler lost; an
        // alternating shake of the same size and decay stands in.
        if (_quakeTicks > 0)
        {
            var sign = _quakeTicks % 2 == 0 ? 1 : -1;
            position += (Vector3.Cross(forward, up) * _quakeX * sign) + (Vector3.Up * _quakeY * sign);
        }

        return Matrix.CreateLookAt(position, position + forward, up);
    }

    private bool Turn(int sense)
    {
        if (IsBusy)
        {
            return false;
        }

        // R1 orbits the camera counterclockwise seen from above (from the south side to the east), L1 the other way.
        // The first step shows on the press tick.
        Mode = (Mode + sense + 4) % 4;
        _targetYaw = _yaw + (sense * 90);
        _turnStep = sense * (_overhead ? OverheadTurnStep : NormalTurnStep);
        _yaw += _turnStep;
        _turnTicksLeft = (_overhead ? OverheadTurnTicks : NormalTurnTicks) - 1;
        return true;
    }
}
