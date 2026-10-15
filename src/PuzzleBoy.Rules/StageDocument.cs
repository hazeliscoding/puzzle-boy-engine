using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace PuzzleBoy.Rules;

/// <summary>A stage in the portable JSON format, schema version 1; <c>docs/stage-json.md</c> describes it.</summary>
/// <param name="Stage">The stage. Its characters' slots aren't stored: they come back as 0 up, in order.</param>
/// <param name="Title">The stage's name as its author wrote it, or null for none.</param>
public sealed record StageDocument(Stage Stage, string? Title)
{
    private const string Format = "puzzle-boy-stage";
    private const int SchemaVersion = 1;
    private const int MaxBytes = 64 * 1024;
    private const string TileLegend = "# . o b B c C a A";
    private const string GroupDigits = ".123456789abcdefghijklmnopqrstuvwxyz";

    /// <summary>Reads a stage file.</summary>
    /// <param name="json">The file's text, at most 64 KiB as UTF-8. Properties the format doesn't define are ignored.</param>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> is null.</exception>
    /// <exception cref="StageFormatException">
    /// The text isn't a playable stage: too large, not JSON, the wrong format or schema version, a missing or malformed
    /// property, or a stage that breaks a rule <see cref="StageBuilder.Validate"/> checks. Nothing else is thrown.
    /// </exception>
    public static StageDocument Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        // RFC 8259 lets a parser skip a byte order mark; Encoding.UTF8.GetString keeps one from a file saved with it.
        if (json.StartsWith('\uFEFF'))
        {
            json = json[1..];
        }

        // Encoding first replaces lone surrogates, which JsonDocument would reject with an ArgumentException.
        var utf8 = Encoding.UTF8.GetBytes(json);
        if (utf8.Length > MaxBytes)
        {
            throw new StageFormatException($"This file is over {MaxBytes / 1024} KiB, too large to be a stage.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(utf8, new JsonDocumentOptions { MaxDepth = 8, AllowDuplicateProperties = false });
        }
        catch (JsonException e)
        {
            // A duplicate property has no line number.
            var where = e.LineNumber is { } line ? $" (line {line + 1})" : "";
            throw new StageFormatException($"This isn't a stage file: it isn't valid JSON{where}.");
        }
        catch (InvalidOperationException)
        {
            // The duplicate-property check unescapes names, so an escaped lone surrogate in one throws here.
            throw NotUnicode();
        }

        using (document)
        {
            try
            {
                return Read(document.RootElement);
            }
            catch (InvalidOperationException)
            {
                // JsonDocument accepts an escaped lone surrogate such as \uD800; reading that string throws.
                throw NotUnicode();
            }
        }

        static StageFormatException NotUnicode() => new("This isn't a stage file: it holds text that isn't valid Unicode.");
    }

    /// <summary>The stage as indented JSON with LF line endings, which <see cref="Parse"/> reads back.</summary>
    /// <remarks>A lone surrogate in the title is written as U+FFFD.</remarks>
    /// <exception cref="InvalidOperationException">
    /// A group is above 35, the most the format can write, or the title makes the file larger than the 64 KiB
    /// <see cref="Parse"/> accepts.
    /// </exception>
    public string ToJson()
    {
        var tiles = new List<string>();
        var groups = new List<string>();
        for (var y = 0; y < Stage.Height; y++)
        {
            var tileRow = new char[Stage.Width];
            var groupRow = new char[Stage.Width];
            for (var x = 0; x < Stage.Width; x++)
            {
                var tile = Tile.FromCell(Stage[x, y]);
                tileRow[x] = TileChar(tile);
                groupRow[x] = tile.Group < GroupDigits.Length
                    ? GroupDigits[tile.Group]
                    : throw new InvalidOperationException($"Group {tile.Group} at ({x}, {y}) is above {GroupDigits.Length - 1}, the most a stage file can hold.");
            }

            tiles.Add(new string(tileRow));
            groups.Add(new string(groupRow));
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, NewLine = "\n", Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartObject();
            writer.WriteString("format", Format);
            writer.WriteNumber("schemaVersion", SchemaVersion);
            if (Title is not null)
            {
                writer.WriteString("title", Title);
            }

            // Pairs stay on one line, so a file reads like the drawing it is.
            writer.WritePropertyName("goal");
            writer.WriteRawValue($"[{Stage.Goal.X}, {Stage.Goal.Y}]");
            writer.WritePropertyName("characters");
            writer.WriteRawValue($"[{string.Join(", ", Stage.Characters.Select(c => $"[{c.X}, {c.Y}]"))}]");
            WriteRows(writer, "tiles", tiles);
            WriteRows(writer, "groups", groups);
            writer.WriteEndObject();
        }

        // The trailing newline counts toward the limit too.
        if (stream.Length + 1 > MaxBytes)
        {
            throw new InvalidOperationException($"The title makes this stage larger than {MaxBytes / 1024} KiB, the most a stage file can hold.");
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    private static StageDocument Read(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new StageFormatException("This isn't a stage file: it should be a JSON object.");
        }

        if (!root.TryGetProperty("format", out var format) || format.ValueKind != JsonValueKind.String || format.GetString() != Format)
        {
            throw new StageFormatException($"This isn't a Puzzle Boy stage: its \"format\" should be \"{Format}\".");
        }

        if (!root.TryGetProperty("schemaVersion", out var schema) || !TryGetInt(schema, out var version))
        {
            throw new StageFormatException("This stage has no \"schemaVersion\" whole number.");
        }

        if (version > SchemaVersion)
        {
            throw new StageFormatException($"This stage was made by a newer version of Puzzle Boy (schema {version}). This version reads schema {SchemaVersion}.");
        }

        if (version < 1)
        {
            throw new StageFormatException($"This stage has schema version {version}, which doesn't exist.");
        }

        string? title = null;
        if (root.TryGetProperty("title", out var titleElement))
        {
            title = titleElement.ValueKind switch
            {
                JsonValueKind.String => titleElement.GetString(),
                JsonValueKind.Null => null,
                _ => throw new StageFormatException("The stage's \"title\" should be text."),
            };
        }

        var builder = new StageBuilder { Goal = ReadPosition(Property(root, "goal"), "The goal") };
        var characters = Property(root, "characters");
        if (characters.ValueKind != JsonValueKind.Array || characters.GetArrayLength() is < 1 or > 4)
        {
            throw new StageFormatException("\"characters\" should list one to four [x, y] start positions.");
        }

        var slot = 0;
        foreach (var character in characters.EnumerateArray())
        {
            builder.Characters.Add(ReadPosition(character, $"Character {++slot}"));
        }

        var tiles = ReadRows(root, "tiles");
        var groups = ReadRows(root, "groups");
        for (var y = 0; y < Stage.Height; y++)
        {
            for (var x = 0; x < Stage.Width; x++)
            {
                var tile = ReadTile(tiles[y][x])
                    ?? throw new StageFormatException([new($"'{tiles[y][x]}' in \"tiles\" isn't a tile. The tiles are {TileLegend}.", x, y)]);
                var group = GroupDigits.IndexOf(groups[y][x], StringComparison.Ordinal);
                if (group < 0)
                {
                    throw new StageFormatException([new($"'{groups[y][x]}' in \"groups\" isn't a group. Use . for none, then 1 to 9 and a to z.", x, y)]);
                }

                builder[x, y] = tile with { Group = group };
            }
        }

        return new StageDocument(builder.Build(), title);
    }

    private static JsonElement Property(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) ? value : throw new StageFormatException($"The stage has no \"{name}\".");

    private static (int X, int Y) ReadPosition(JsonElement position, string what)
    {
        if (position.ValueKind != JsonValueKind.Array || position.GetArrayLength() != 2
            || !TryGetInt(position[0], out var x) || !TryGetInt(position[1], out var y))
        {
            throw new StageFormatException($"{what} should be a position: two whole numbers [x, y].");
        }

        if (x is < 0 or >= Stage.Width || y is < 0 or >= Stage.Height)
        {
            throw new StageFormatException($"{what} at [{x}, {y}] is off the {Stage.Width}×{Stage.Height} grid.");
        }

        return (x, y);
    }

    private static string[] ReadRows(JsonElement root, string name)
    {
        var rows = Property(root, name);
        if (rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() != Stage.Height)
        {
            throw new StageFormatException($"\"{name}\" should be {Stage.Height} rows of {Stage.Width} characters.");
        }

        var result = new string[Stage.Height];
        var y = 0;
        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.String || row.GetString() is not { Length: Stage.Width } text)
            {
                throw new StageFormatException($"Row {y} of \"{name}\" should be a string of exactly {Stage.Width} characters.");
            }

            result[y++] = text;
        }

        return result;
    }

    private static bool TryGetInt(JsonElement element, out int value)
    {
        value = 0;
        return element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out value);
    }

    private static Tile? ReadTile(char c) => c switch
    {
        '#' => new Tile(CellKind.Wall),
        '.' => new Tile(CellKind.Floor),
        'o' => new Tile(CellKind.Hole),
        'b' or 'B' => new Tile(CellKind.Block, OverHole: c == 'B'),
        'c' or 'C' => new Tile(CellKind.SwivelCenter, OverHole: c == 'C'),
        'a' or 'A' => new Tile(CellKind.SwivelArm, OverHole: c == 'A'),
        _ => null,
    };

    private static char TileChar(Tile tile) => tile.Kind switch
    {
        CellKind.Wall => '#',
        CellKind.Floor => '.',
        CellKind.Hole => 'o',
        CellKind.Block => tile.OverHole ? 'B' : 'b',
        CellKind.SwivelCenter => tile.OverHole ? 'C' : 'c',
        _ => tile.OverHole ? 'A' : 'a',
    };

    private static void WriteRows(Utf8JsonWriter writer, string name, List<string> rows)
    {
        writer.WriteStartArray(name);
        foreach (var row in rows)
        {
            writer.WriteStringValue(row);
        }

        writer.WriteEndArray();
    }
}
