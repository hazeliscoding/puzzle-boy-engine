using Microsoft.Xna.Framework.Audio;
using PuzzleBoy.Nocturne;

namespace PuzzleBoy.Rendering;

/// <summary>
/// The minigame's sound effects and music from the player's install (puzzle-boy's ROADMAP.md, "M3 findings": sound).
/// Every effect plays at full volume, as in the original.
/// </summary>
public sealed class Sounds : IDisposable
{
    private const string Music = "JYAKYO02_BGM00_L_";

    private readonly Dictionary<string, SoundEffect> _effects = [];
    private SoundEffectInstance? _music;

    /// <summary>Creates the effects; on a machine with no audio device the game stays silent instead of failing.</summary>
    public Sounds(IReadOnlyDictionary<string, SoundData> sounds)
    {
        try
        {
            foreach (var (name, sound) in sounds)
            {
                _effects[name] = new SoundEffect(sound.Pcm, sound.SampleRate, sound.Channels == 1 ? AudioChannels.Mono : AudioChannels.Stereo);
            }
        }
        catch (NoAudioHardwareException)
        {
            // shortcut: no message when there's no audio device; one comes with the in-game error screen, if ever.
            _effects.Clear();
        }
    }

    /// <summary>Plays effect <c>JYAKYO02_SE</c><paramref name="number"/>: 0 step, 1 push, 2 turn, 3 fall, 8 view, 9 goal.</summary>
    public void Play(int number)
    {
        if (_effects.TryGetValue($"JYAKYO02_SE{number:D2}", out var effect))
        {
            effect.Play();
        }
    }

    /// <summary>Starts the music. The <c>_L_</c> in its name marks a looping track, so it loops whole.</summary>
    public void StartMusic()
    {
        if (_music is null && _effects.TryGetValue(Music, out var music))
        {
            _music = music.CreateInstance();
            _music.IsLooped = true;
            _music.Play();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _music?.Dispose();
        foreach (var effect in _effects.Values)
        {
            effect.Dispose();
        }
    }
}
