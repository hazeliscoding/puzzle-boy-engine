# Stage JSON

A stage authored for Puzzle Boy is a small JSON file. `StageDocument.Parse` reads it and `StageDocument.ToJson` writes it. This is schema version 1.

```json
{
  "format": "puzzle-boy-stage",
  "schemaVersion": 1,
  "title": "First Push",
  "goal": [4, 1],
  "characters": [[0, 1], [2, 0]],
  "tiles": [
    "...o.###############",
    ".b.A.###############",
    ".bac.###############",
    "####################",
    "... 20 rows in all ..."
  ],
  "groups": [
    "....................",
    ".1.w................",
    ".1ww................",
    "....................",
    "... 20 rows in all ..."
  ]
}
```

## Properties

| Property | Required | Meaning |
|---|---|---|
| `format` | yes | Always `"puzzle-boy-stage"`. |
| `schemaVersion` | yes | `1`. A higher number fails with "made by a newer version". |
| `title` | no | The stage's name, or `null`. |
| `goal` | yes | The goal's cell, `[x, y]`. |
| `characters` | yes | One to four start cells, `[[x, y], ...]`. The first starts in control. |
| `tiles` | yes | 20 strings of exactly 20 tiles. |
| `groups` | yes | 20 strings of exactly 20 group marks, matching `tiles` cell for cell. |

Other properties are ignored. A file is at most 64 KiB.

## Coordinates

`[x, y]` is column x, row y. Row 0 is the first string in `tiles`, and x = 0 is its first character. This matches the drawings in the engine's tests.

The game draws +y up the screen, so a stage appears on screen mirrored top to bottom from how the file reads.

## Tiles

| Mark | Tile |
|---|---|
| `#` | wall |
| `.` | floor |
| `o` | hole |
| `b` / `B` | block / block resting on a hole |
| `c` / `C` | swivel center / center resting on a hole |
| `a` / `A` | swivel arm / arm resting on a hole |

## Groups

`.` is no group. `1` to `9`, then `a` to `z`, are groups 1 to 35. Every block and swivel cell needs a group and every other tile has none. The engine allows groups up to 2047, but this format writes only 35.

## Rules

A file must also follow the rules for authored stages, which `StageBuilder.Validate` checks. Every broken rule is reported at once, with the cell it's about:

- exactly one goal, on floor;
- one to four characters, each on floor, none on the goal, no two on one cell;
- a group is all blocks or all one swivel, never both;
- a block group's cells touch side to side;
- a swivel has exactly one center and at least one arm, and each arm is in the center's row or column with only that swivel's arms in between.

Anything else wrong, such as a missing property, a value of the wrong type, a cell off the grid or an unknown mark, fails with one message. Every failure is a `StageFormatException`.
