using System.Text.Json.Nodes;

namespace PuzzleBoy.Rules.Tests;

public sealed class StageDocumentTests
{
    private const string Drawing = """
        ..2o4
        1a.HG
        .aRC.
        """;

    // The whole file, as the format promises to write it.
    private const string Json = """
        {
          "format": "puzzle-boy-stage",
          "schemaVersion": 1,
          "title": "First Push",
          "goal": [4, 1],
          "characters": [[0, 1], [2, 0], [4, 0]],
          "tiles": [
            "...o.###############",
            ".b.A.###############",
            ".bac.###############",
            "####################",
            "####################",
            "####################",
            "####################",
            "####################",
            "####################",
            "####################",
            "####################",
            "####################",
            "####################",
            "####################",
            "####################",
            "####################",
            "####################",
            "####################",
            "####################",
            "####################"
          ],
          "groups": [
            "....................",
            ".1.w................",
            ".1ww................",
            "....................",
            "....................",
            "....................",
            "....................",
            "....................",
            "....................",
            "....................",
            "....................",
            "....................",
            "....................",
            "....................",
            "....................",
            "....................",
            "....................",
            "....................",
            "....................",
            "...................."
          ]
        }

        """;

    [Fact]
    public void ToJson_writes_the_documented_layout()
    {
        var stage = Stage();

        Assert.Equal(Json, new StageDocument(stage, "First Push").ToJson());
    }

    [Fact]
    public void A_document_round_trips_through_json()
    {
        var stage = Stage();

        var read = StageDocument.Parse(new StageDocument(stage, "Ünïcode + \"quotes\"").ToJson());

        Assert.Equal("Ünïcode + \"quotes\"", read.Title);
        Assert.Equal(stage.Goal, read.Stage.Goal);
        Assert.Equal(stage.Characters, read.Stage.Characters);
        Assert.Equal(stage.ToFile(), read.Stage.ToFile());
    }

    [Fact]
    public void A_missing_or_null_title_reads_as_none()
    {
        Assert.Null(StageDocument.Parse(new StageDocument(Stage(), null).ToJson()).Title);
        Assert.Null(StageDocument.Parse(Edit(j => j["title"] = null)).Title);
    }

    [Fact]
    public void Unknown_properties_are_ignored() =>
        Assert.Equal("First Push", StageDocument.Parse(Edit(j => j["author"] = new JsonObject { ["name"] = "someone" })).Title);

    [Fact]
    public void ToJson_refuses_a_group_the_format_cannot_write()
    {
        var builder = Ascii.Builder("1.G");
        builder[1, 1] = new Tile(CellKind.Block, 36);

        Assert.Throws<InvalidOperationException>(() => new StageDocument(builder.Build(), null).ToJson());
    }

    [Theory]
    [InlineData("", "This isn't a stage file: it isn't valid JSON (line 1).")]
    [InlineData("{\n\"format\": ", "This isn't a stage file: it isn't valid JSON (line 2).")]
    [InlineData("not json", "This isn't a stage file: it isn't valid JSON (line 1).")]
    [InlineData("[]", "This isn't a stage file: it should be a JSON object.")]
    [InlineData("{\"format\": \"puzzle-boy-stage\", \"format\": \"puzzle-boy-stage\"}", "This isn't a stage file: it isn't valid JSON.")]
    public void Text_that_is_not_a_json_object_is_rejected(string text, string expected) =>
        Assert.Equal(expected, Reject(text));

    [Fact]
    public void A_lone_surrogate_is_read_as_a_replacement_character_not_an_exception() =>
        Assert.Equal("This isn't a stage file: it should be a JSON object.", Reject("\"\uD800\""));

    // JsonDocument accepts an escaped lone surrogate; reading the string later throws InvalidOperationException.
    [Theory]
    [InlineData("\"format\": \"puzzle-boy-stage\"", "\"format\": \"\\uD800\"")]
    [InlineData("\"First Push\"", "\"First \\uDC00Push\"")]
    [InlineData("\"format\":", "\"\\uD800\": 1, \"format\":")]
    [InlineData("\"....................\"", "\"...................\\uD800\"")]
    public void An_escaped_lone_surrogate_is_rejected(string from, string to) =>
        Assert.Equal("This isn't a stage file: it holds text that isn't valid Unicode.", Reject(Json.Replace(from, to, StringComparison.Ordinal)));

    [Fact]
    public void Nesting_too_deep_is_rejected() =>
        Assert.Equal("This isn't a stage file: it isn't valid JSON (line 1).", Reject(Edit(j => j["extra"] = JsonNode.Parse(new string('[', 50) + new string(']', 50)))));

    [Fact]
    public void A_file_over_64_KiB_is_rejected() =>
        Assert.Equal("This file is over 64 KiB, too large to be a stage.", Reject(Edit(j => j["padding"] = new string(' ', 64 * 1024))));

    [Theory]
    [InlineData("puzzle-boy-pack")]
    [InlineData(null)]
    public void The_wrong_format_is_rejected(string? format) =>
        Assert.Equal("This isn't a Puzzle Boy stage: its \"format\" should be \"puzzle-boy-stage\".", Reject(Edit(j => j["format"] = format)));

    [Fact]
    public void A_newer_schema_version_is_rejected() =>
        Assert.Equal("This stage was made by a newer version of Puzzle Boy (schema 2). This version reads schema 1.", Reject(Edit(j => j["schemaVersion"] = 2)));

    [Fact]
    public void A_schema_version_below_1_is_rejected() =>
        Assert.Equal("This stage has schema version 0, which doesn't exist.", Reject(Edit(j => j["schemaVersion"] = 0)));

    [Theory]
    [InlineData("[20, 0]", "The goal at [20, 0] is off the 20×20 grid.")]
    [InlineData("[0, -1]", "The goal at [0, -1] is off the 20×20 grid.")]
    [InlineData("[2147483648, 0]", "The goal should be a position: two whole numbers [x, y].")]
    [InlineData("[1.5, 2]", "The goal should be a position: two whole numbers [x, y].")]
    [InlineData("[1]", "The goal should be a position: two whole numbers [x, y].")]
    [InlineData("[1, 2, 3]", "The goal should be a position: two whole numbers [x, y].")]
    public void A_goal_that_is_not_a_cell_is_rejected(string goal, string expected) =>
        Assert.Equal(expected, Reject(Edit(j => j["goal"] = JsonNode.Parse(goal))));

    [Theory]
    [InlineData("[]", "\"characters\" should list one to four [x, y] start positions.")]
    [InlineData("[[0, 1], [2, 0], [4, 0], [1, 0], [3, 0]]", "\"characters\" should list one to four [x, y] start positions.")]
    [InlineData("[[0, 1], [0, 20]]", "Character 2 at [0, 20] is off the 20×20 grid.")]
    [InlineData("[[0, 1], \"2, 0\"]", "Character 2 should be a position: two whole numbers [x, y].")]
    public void Characters_out_of_range_are_rejected(string characters, string expected) =>
        Assert.Equal(expected, Reject(Edit(j => j["characters"] = JsonNode.Parse(characters))));

    [Theory]
    [InlineData("tiles", 19)]
    [InlineData("tiles", 21)]
    [InlineData("groups", 19)]
    public void The_wrong_number_of_rows_is_rejected(string name, int rows) =>
        Assert.Equal(
            $"\"{name}\" should be 20 rows of 20 characters.",
            Reject(Edit(j => j[name] = new JsonArray([.. Enumerable.Repeat(0, rows).Select(_ => (JsonNode?)new string('#', 20))]))));

    [Theory]
    [InlineData(19)]
    [InlineData(21)]
    public void A_row_of_the_wrong_length_is_rejected(int length) =>
        Assert.Equal("Row 3 of \"tiles\" should be a string of exactly 20 characters.", Reject(Edit(j => j["tiles"]![3] = new string('#', length))));

    [Fact]
    public void A_row_that_is_not_a_string_is_rejected() =>
        Assert.Equal("Row 0 of \"groups\" should be a string of exactly 20 characters.", Reject(Edit(j => j["groups"]![0] = 5)));

    [Fact]
    public void An_unknown_tile_is_rejected_at_its_cell()
    {
        var error = Assert.Throws<StageFormatException>(() => StageDocument.Parse(Edit(j => j["tiles"]![3] = "###x################")));

        Assert.Equal("(3, 3): 'x' in \"tiles\" isn't a tile. The tiles are # . o b B c C a A.", Assert.Single(error.Problems).ToString());
        Assert.Equal((3, 3), (error.Problems[0].X, error.Problems[0].Y));
    }

    [Fact]
    public void An_unknown_group_is_rejected_at_its_cell() =>
        Assert.Equal("(5, 4): 'Z' in \"groups\" isn't a group. Use . for none, then 1 to 9 and a to z.", Reject(Edit(j => j["groups"]![4] = ".....Z..............")));

    [Fact]
    public void A_group_on_a_plain_tile_is_rejected_by_validation() =>
        Assert.Equal("(0, 0): This floor has group 1, but only blocks and swivel parts belong to a group.", Reject(Edit(j => j["groups"]![0] = "1...................")));

    [Fact]
    public void A_block_without_a_group_is_rejected_by_validation() =>
        Assert.Equal("(1, 1): This block has no group. Give it a group from 1 to 2047.", Reject(Edit(j => j["groups"]![1] = "...w................")));

    [Fact]
    public void Every_validation_problem_is_reported_together()
    {
        var error = Assert.Throws<StageFormatException>(() => StageDocument.Parse(Edit(j =>
        {
            j["goal"] = JsonNode.Parse("[0, 3]");
            j["characters"] = JsonNode.Parse("[[0, 1], [0, 1]]");
        })));

        Assert.Equal(
            [
                "(0, 3): The goal is on a wall. It needs a floor tile.",
                "(0, 1): Character 2 starts on the same cell as character 1.",
            ],
            error.Problems.Select(p => p.ToString()));
    }

    public static TheoryData<string> Properties => ["format", "schemaVersion", "title", "goal", "characters", "tiles", "groups"];

    // Any value of any JSON kind in any property either reads or fails with StageFormatException, never anything else.
    [Theory]
    [MemberData(nameof(Properties))]
    public void Any_value_in_any_property_fails_only_with_StageFormatException(string property)
    {
        string[] values = ["null", "true", "0", "-1", "1.5", "1e400", "\"x\"", "[]", "[[]]", "[null]", "[[1, \"a\"]]", "{}", "[\"#\"]", "[1, 2]"];
        foreach (var value in values)
        {
            AssertOnlyStageFormatException(Edit(j => j[property] = JsonNode.Parse(value)));
        }

        AssertOnlyStageFormatException(Edit(j => j.Remove(property)));
    }

    private static void AssertOnlyStageFormatException(string json)
    {
        var error = Record.Exception(() => StageDocument.Parse(json));
        Assert.True(error is null or StageFormatException, $"{error?.GetType().Name} for {json}");
    }

    private static Stage Stage() => StageBuilder.From(Ascii.Stage(Drawing)).Build();

    private static string Edit(Action<JsonObject> edit)
    {
        var json = JsonNode.Parse(Json)!.AsObject();
        edit(json);
        return json.ToJsonString();
    }

    private static string Reject(string json) =>
        Assert.Throws<StageFormatException>(() => StageDocument.Parse(json)).Message;
}
