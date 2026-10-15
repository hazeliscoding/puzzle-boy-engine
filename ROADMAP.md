# Roadmap

Puzzle Boy Engine is the headless rules of the Puzzle Boy minigame, shared by the preservation port and Community Edition. This file tracks the decisions already made, what gets built and in what order.

## Decisions (2026.10.14)

- **Copied, not extracted with history.** The rules and their tests were copied from puzzle-boy at `410fc89` (unchanged at `3a06920`, where the split started). Git history was not carried over; puzzle-boy keeps it, along with the research the rules come from.
- **Same API, minus the play order.** `Board`, `Stage`, `Cell`, `CellKind`, `Direction`, `Directions`, `StepResult`, `Move` and `Character` keep their exact meaning, and the namespace stays `PuzzleBoy.Rules`. `PlayOrder` left the engine: the original 20-file order belongs to puzzle-boy, the bonus order to Community.
- **`Board.Switch()` stays.** The original has no switch, but Community's multi-character stages need it. The preservation port no longer calls it.
- **`Stage.Parse` stays lenient.** It accepts anything the original's files hold, including group ids that mix blocks and swivels. Validation applies only to stages authored for Community.
- **Consumers pin a commit.** Both games use this repo as a git submodule at `external/puzzle-boy-engine` with a project reference. No NuGet package for now.
- **CI on three systems.** The rules have no platform code, and CI proves it on Linux, Windows and macOS.

## Decisions (2026.10.15)

- **Authoring API.** `Tile`, `StageProblem`, `StageFormatException`, `StageBuilder`, `Stage.ToFile()` and `StageDocument` are new and additive; nothing existing changed meaning. A builder starts as all wall, and characters take slots 0 up in order, so `StageBuilder.From` closes the gaps of a stage that skips slots.
- **`StageFormatException` derives from `FormatException`.** The plan was `InvalidDataException`, which `Stage.Parse` throws, but that class is sealed. A caller that wants both catches both.
- **Validation rules** (authored stages only): one goal on floor; one to four characters, each on floor, off the goal and on its own cell; a group is all blocks or all one swivel; a block group touches side to side; a swivel has one center and at least one arm, each in line with the center with only its own arms between. Plain tiles have no group and no hole flag under them. Messages are full sentences for the player and point at a cell.
- **Stage JSON, schema version 1** (`docs/stage-json.md`). Tiles and groups are 20 strings of 20 characters each, so a file reads like a drawing; coordinates are `[x, y]` with row 0 first, like the tests. Groups 1–35 only. At most 64 KiB, strict JSON, no duplicate properties, nesting at most 8 deep, unknown properties ignored. Every malformed input fails with `StageFormatException`; a property-by-property sweep of every JSON value kind checks it.

## E1: Extract the rules (done 2026.10.14)

- [x] Copy `PuzzleBoy.Rules` without `PlayOrder`, and the rule-only tests, from puzzle-boy.
  - 86 test cases moved from puzzle-boy's 164: Cell 16, Goal 3, LastMove 5, Pushing 13, Stage 6, Switch 5, Swivel 22, Undo 10, Walking 6, plus the `Ascii` helper. The other 78, which touch the game, stay there.
- [x] Copy the no-game-data check and its positive control.
- [x] CI: build, test and format on Linux, Windows and macOS; the guard on Linux.

**Done when** every rule test that used to run in puzzle-boy runs here, without MonoGame, a game install or Nocturne data, and every CI gate passes.

## E2: Authoring model (done 2026.10.15)

- [x] An authorable stage: build one in code, validate it with messages a player can act on, and write it as an original-layout file.
  - Every drawn test stage also goes through `StageBuilder.From` and back to raw cells, so the builder keeps every stage the rules tests use.
- [x] A portable JSON stage format, documented in `docs/stage-json.md`, that rejects every malformed input with one exception type.
- [x] Regression tests that pin the behavior consumers rely on.
  - The same stage built in code, drawn through `Stage.Parse`, written with `ToFile` and read from JSON leaves identical boards after each of 28 inputs that walk, push, fall, turn, switch, undo, refuse and retry.
  - `Board.Switch()` stays public, `Stage.Parse` still accepts stages the builder rejects and still throws `InvalidDataException`, and the exported types are pinned by name.

**Done when** a stage built in code, parsed from bytes or read from JSON plays the same, and every validation rule and malformed input has a test.

## Later

- A NuGet package, if a third consumer appears.
- A shared stage solver for hints or checking packs, if both games need one.

## Not planned

- Rendering, audio, input or asset reading. They belong to each game.
- Any Atlus data, including original stage layouts converted to JSON.
- Behavior the original game doesn't have, beyond what Community needs and the documented differences.
