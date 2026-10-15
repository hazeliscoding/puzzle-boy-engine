using System.Buffers.Binary;
using System.Text.RegularExpressions;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Fmod5Sharp;
using Microsoft.Win32;
using NVorbis;

namespace PuzzleBoy.Nocturne;

/// <summary>A texture from the game, every mip level as RGBA bytes in Unity's row order: the bottom row first.</summary>
/// <param name="Width">The top level's width in pixels.</param>
/// <param name="Height">The top level's height in pixels.</param>
/// <param name="Levels">The mip levels, largest first, each half the size of the one before.</param>
public sealed record TextureData(int Width, int Height, IReadOnlyList<byte[]> Levels);

/// <summary>A sound from the game as 16-bit little-endian PCM, channels interleaved.</summary>
/// <param name="Pcm">The samples.</param>
/// <param name="SampleRate">Samples per second per channel.</param>
/// <param name="Channels">1 or 2.</param>
public sealed record SoundData(byte[] Pcm, int SampleRate, int Channels)
{
    /// <summary>Converts float samples (-1 to 1) to 16-bit PCM, clamping anything louder.</summary>
    public static byte[] Pcm16(ReadOnlySpan<float> samples)
    {
        var pcm = new byte[samples.Length * 2];
        for (var i = 0; i < samples.Length; i++)
        {
            var value = (short)Math.Clamp(MathF.Round(samples[i] * short.MaxValue), short.MinValue, short.MaxValue);
            BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(2 * i), value);
        }

        return pcm;
    }
}

/// <summary>The player's copy of Nocturne HD Remaster. Every asset the port uses is read from it.</summary>
public sealed partial class GameInstall
{
    private GameInstall(string directory) => Directory = directory;

    /// <summary>The game's folder, the one that holds <c>smt3hd.exe</c> and <c>smt3hd_Data</c>.</summary>
    public string Directory { get; }

    /// <summary>The game at <paramref name="directory"/>, or null when its Puzzle Boy bundle isn't there.</summary>
    public static GameInstall? At(string directory) =>
        File.Exists(BundlePath(directory, "pazzleboy")) ? new GameInstall(directory) : null;

    /// <summary>Looks for the game in every Steam library on this machine.</summary>
    public static GameInstall? Find() =>
        SteamRoots()
            .SelectMany(root => LibraryFolders(root).Prepend(root))
            .Select(library => At(Path.Combine(library, "steamapps", "common", "smt3hd")))
            .FirstOrDefault(install => install is not null);

    /// <summary>The library paths listed in Steam's <c>libraryfolders.vdf</c>.</summary>
    public static IEnumerable<string> LibraryPaths(string libraryFoldersVdf) =>
        PathEntry().Matches(libraryFoldersVdf).Select(match => match.Groups[1].Value.Replace(@"\\", @"\"));

    /// <summary>Every TextAsset in one of the game's bundles, by name.</summary>
    public Dictionary<string, byte[]> ReadTextAssets(string bundle)
    {
        var manager = new AssetsManager();
        try
        {
            var bundleFile = manager.LoadBundleFile(BundlePath(Directory, bundle), true);
            var assets = manager.LoadAssetsFileFromBundle(bundleFile, 0, false);
            return assets.file.GetAssetsOfType(AssetClassID.TextAsset)
                .Select(info => manager.GetBaseField(assets, info))
                .ToDictionary(field => field["m_Name"].AsString, field => field["m_Script"].AsByteArray);
        }
        finally
        {
            manager.UnloadAll();
        }
    }

    /// <summary>
    /// Texture2D <paramref name="name"/> from one of the game's bundles, every mip level decoded to RGBA.
    /// Throws <see cref="InvalidDataException"/> when it's missing or in a format the port doesn't read (DXT1, RGB24 and RGBA32 only).
    /// </summary>
    public TextureData ReadTexture(string bundle, string name) => ReadTextures(bundle, name)[name];

    /// <summary>
    /// Several textures from one bundle, by name, opening it once: a large bundle like <c>common</c> takes a while to
    /// unpack. Throws as <see cref="ReadTexture"/> does.
    /// </summary>
    public Dictionary<string, TextureData> ReadTextures(string bundle, params string[] names)
    {
        var manager = new AssetsManager();
        try
        {
            var bundleFile = manager.LoadBundleFile(BundlePath(Directory, bundle), true);
            var assets = manager.LoadAssetsFileFromBundle(bundleFile, 0, false);
            var found = assets.file.GetAssetsOfType(AssetClassID.Texture2D)
                .Select(info => manager.GetBaseField(assets, info))
                .Where(field => names.Contains(field["m_Name"].AsString))
                .GroupBy(field => field["m_Name"].AsString)
                .ToDictionary(group => group.Key, group => DecodeTexture(bundleFile, group.First(), bundle));
            var missing = names.FirstOrDefault(name => !found.ContainsKey(name));
            return missing is null ? found : throw new InvalidDataException($"There's no texture {missing} in {bundle}.");
        }
        finally
        {
            manager.UnloadAll();
        }
    }

    /// <summary>Decodes a Texture2D's every mip level; see <see cref="ReadTexture"/>.</summary>
    private static TextureData DecodeTexture(BundleFileInstance bundleFile, AssetTypeValueField texture, string bundle)
    {
        // Large textures keep their pixels in the bundle's .resS entry; small ones inline.
        var stream = texture["m_StreamData"];
        var data = stream["size"].AsUInt > 0
            ? ReadEntry(bundleFile, stream["path"].AsString, stream["offset"].AsULong, stream["size"].AsUInt)
            : texture["image data"].AsByteArray;

        var (width, height, format) = (texture["m_Width"].AsInt, texture["m_Height"].AsInt, texture["m_TextureFormat"].AsInt);
        var levels = new List<byte[]>();
        var at = 0;
        for (var level = 0; level < texture["m_MipCount"].AsInt; level++)
        {
            var (w, h) = (Math.Max(1, width >> level), Math.Max(1, height >> level));
            var (pixels, size) = format switch
            {
                10 => (TextureDecoder.Dxt1(data.AsSpan(at), w, h), TextureDecoder.Dxt1Size(w, h)),
                3 => (TextureDecoder.Rgb24(data.AsSpan(at), w, h), w * h * 3),
                4 => (data.AsSpan(at, w * h * 4).ToArray(), w * h * 4),
                _ => throw new InvalidDataException($"Texture {texture["m_Name"].AsString} in {bundle} has format {format}, which the port doesn't read."),
            };
            levels.Add(pixels);
            at += size;
        }

        return new TextureData(width, height, levels);
    }

    /// <summary>
    /// Every AudioClip in one of the game's bundles, by name, decoded to 16-bit PCM. The clips are Vorbis inside
    /// FMOD sound banks (FSB5). Throws <see cref="InvalidDataException"/> for a clip in another codec.
    /// </summary>
    public Dictionary<string, SoundData> ReadSounds(string bundle)
    {
        var manager = new AssetsManager();
        try
        {
            var bundleFile = manager.LoadBundleFile(BundlePath(Directory, bundle), true);
            var assets = manager.LoadAssetsFileFromBundle(bundleFile, 0, false);
            var sounds = new Dictionary<string, SoundData>();
            foreach (var clip in assets.file.GetAssetsOfType(AssetClassID.AudioClip).Select(info => manager.GetBaseField(assets, info)))
            {
                var name = clip["m_Name"].AsString;
                var resource = clip["m_Resource"];
                var bank = FsbLoader.LoadFsbFromByteArray(ReadEntry(bundleFile, resource["m_Source"].AsString, resource["m_Offset"].AsULong, (uint)resource["m_Size"].AsULong));
                if (!bank.Samples[0].RebuildAsStandardFileFormat(out var ogg, out var extension) || extension != "ogg")
                {
                    throw new InvalidDataException($"Sound {name} in {bundle} isn't Vorbis, which the port doesn't read.");
                }

                using var reader = new VorbisReader(new MemoryStream(ogg!), true);
                var samples = new float[reader.TotalSamples * reader.Channels];
                var read = 0;
                while (read < samples.Length && reader.ReadSamples(samples, read, samples.Length - read) is > 0 and var n)
                {
                    read += n;
                }

                sounds[name] = new SoundData(SoundData.Pcm16(samples.AsSpan(0, read)), reader.SampleRate, reader.Channels);
            }

            return sounds;
        }
        finally
        {
            manager.UnloadAll();
        }
    }

    /// <summary>Reads <paramref name="size"/> bytes at <paramref name="offset"/> of a bundle entry named by a Unity resource path.</summary>
    private static byte[] ReadEntry(BundleFileInstance bundle, string path, ulong offset, uint size)
    {
        var file = bundle.file;
        file.GetFileRange(file.GetFileIndex(Path.GetFileName(path)), out var start, out _);
        file.DataReader.Position = start + (long)offset;
        return file.DataReader.ReadBytes((int)size);
    }

    private static string BundlePath(string directory, string bundle) =>
        Path.Combine(directory, "smt3hd_Data", "StreamingAssets", "PC", bundle);

    private static IEnumerable<string> LibraryFolders(string steamRoot)
    {
        var vdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        return File.Exists(vdf) ? LibraryPaths(File.ReadAllText(vdf)) : [];
    }

    private static IEnumerable<string> SteamRoots()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsWindows())
        {
            if (Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) is string steamPath)
            {
                yield return steamPath;
            }

            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
        }
        else if (OperatingSystem.IsMacOS())
        {
            yield return Path.Combine(home, "Library", "Application Support", "Steam");
        }
        else
        {
            // Native, the Debian package's symlink, and Flatpak. The game itself runs under Proton there.
            yield return Path.Combine(home, ".local", "share", "Steam");
            yield return Path.Combine(home, ".steam", "steam");
            yield return Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam");
        }
    }

    [GeneratedRegex("\"path\"\\s+\"([^\"]+)\"")]
    private static partial Regex PathEntry();
}
