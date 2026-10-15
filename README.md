# Puzzle Boy Engine

**Reusable headless puzzle rules and stage authoring engine. Not a playable game.**

`PuzzleBoy.Rules` is the rules of the Puzzle Boy minigame from *Shin Megami Tensei III Nocturne HD Remaster*: walking, block pushes, swivel turns, holes, the goal, undo and retry. It has no graphics, no audio and no file access, so tests drive it with plain bytes. Two games build on it:

- [puzzle-boy](https://github.com/hazeliscoding/puzzle-boy), a faithful preservation port that reads the original 20 stages from your own copy of Nocturne.
- [puzzle-boy-community](https://github.com/hazeliscoding/puzzle-boy-community), a standalone puzzle game with its own stages that needs no Nocturne.

This repository contains no Atlus code or data. Every stage in it is handmade.

## Architecture

One library, one test project, .NET 10:

- `src/PuzzleBoy.Rules`: the rules, namespace `PuzzleBoy.Rules`. No dependencies.
- `tests/PuzzleBoy.Rules.Tests`: xunit.v3 tests on handmade stages drawn as text.

The rules are instant and time belongs to the game. `Board.Step` applies a whole move at once and records what moved in `LastMove`; a game animates that afterwards.

## API overview

- `Stage` is a stage as the original stores it: a 20×20 grid of 16-bit cells, 1–4 character starts and a goal. `Stage.Parse(ReadOnlySpan<byte>)` reads the original's 828-byte files, repairing the CRLF damage six of them shipped with.
- `Cell` and `CellKind` read a raw cell value: its kind, its block or swivel group and the hole flag.
- `Board` is a stage in play. `Step(Direction)` walks, pushes or turns and returns a `StepResult`; `Undo()`, `Retry()` and `Switch()` do what they say. `Characters`, `Active`, `IsCleared`, `CanUndo` and `LastMove` describe the state.
- `Direction`, `Directions`, `StepResult`, `Move` and `Character` are the values those members use.

For authoring new stages:

- `Tile` is one cell as an editor shows it: a `CellKind`, a group and whether a block or swivel part rests on a hole. `ToCell()` and `Tile.FromCell(ushort)` convert to and from raw cells.
- `StageBuilder` is a stage being made: a 20×20 tile grid that starts as all wall, a `Goal` and 1–4 `Characters`. `Validate()` lists every `StageProblem`, each a sentence for the player with the cell it's about; `Build()` returns the `Stage` or throws a `StageFormatException` carrying them all. `StageBuilder.From(Stage)` starts from an existing stage.
- `Stage.ToFile()` writes a stage in the original's 828-byte layout, which `Stage.Parse` reads back.
- `StageDocument` reads and writes the portable JSON format in [docs/stage-json.md](docs/stage-json.md). `Parse` throws only `StageFormatException`, whatever the input.

`Stage.Parse` stays lenient, as the original's files need; only authored stages are validated.

The rules follow the original game's code as read in the preservation port's research (puzzle-boy's `ROADMAP.md`, "M0 findings"), with three documented differences none of the original's stages can show.

## Use it from a game

Consumers pin the engine as a git submodule at `external/puzzle-boy-engine` and reference its project:

```bash
git submodule add https://github.com/hazeliscoding/puzzle-boy-engine.git external/puzzle-boy-engine
```

```xml
<ProjectReference Include="..\..\external\puzzle-boy-engine\src\PuzzleBoy.Rules\PuzzleBoy.Rules.csproj" />
```

To move a consumer to a newer engine, commit here first, then in the consumer:

```bash
git -C external/puzzle-boy-engine fetch
git -C external/puzzle-boy-engine checkout <sha>
git add external/puzzle-boy-engine
git commit -m "build: bump engine to <short-sha>"
```

List the engine commit subjects in the commit body. A consumer never points at uncommitted engine work or a floating `main`. CI checks out with `submodules: recursive`.

## Tests

```bash
dotnet build PuzzleBoy.Engine.slnx
dotnet test --solution PuzzleBoy.Engine.slnx
dotnet format PuzzleBoy.Engine.slnx --verify-no-changes
scripts/check-no-game-data.test.sh && scripts/check-no-game-data.sh
```

CI runs them on Linux, Windows and macOS. No test needs the game installed.

## Origin

The code and tests were copied from [hazeliscoding/puzzle-boy](https://github.com/hazeliscoding/puzzle-boy) at commit `410fc89` (the rules are unchanged at `3a06920`, where the split started). Git history was not carried over, so `git blame` here starts at the copy. The history of every rule, and the research behind it, lives in puzzle-boy.

## License

Apache-2.0, see [LICENSE](LICENSE).

*Shin Megami Tensei*, *Nocturne* and *Puzzle Boy* are trademarks of ATLUS and SEGA. This is a fan project. It is not affiliated with or endorsed by either company.
