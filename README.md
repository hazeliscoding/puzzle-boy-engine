# Puzzle Boy Engine

**Reusable headless puzzle rules and stage authoring engine, plus the libraries both games use to read and draw the player's own Nocturne files. Not a playable game.**

`PuzzleBoy.Rules` is the rules of the Puzzle Boy minigame from *Shin Megami Tensei III Nocturne HD Remaster*: walking, block pushes, swivel turns, holes, the goal, undo and retry. It has no graphics, no audio and no file access, so tests drive it with plain bytes. Two games build on it:

- [puzzle-boy](https://github.com/hazeliscoding/puzzle-boy), a faithful preservation port that reads the original 20 stages from your own copy of Nocturne.
- [puzzle-boy-community](https://github.com/hazeliscoding/puzzle-boy-community), the Nocturne minigame expanded, with new stages and an editor. Its Classic theme comes from your own copy, and it plays out of the box with its bundled Default theme.

`PuzzleBoy.Nocturne` and `PuzzleBoy.Rendering` are the preservation port's asset reading and classic presentation, shared so both games show the original's board, Jack-o'-Lantern, camera, timing and sound the same way. They read the player's installed copy at runtime.

This repository contains no Atlus code or data, and never will. Every stage and every test asset in it is handmade.

## Architecture

Three libraries, each with a test project, .NET 10:

- `src/PuzzleBoy.Rules`: the rules, namespace `PuzzleBoy.Rules`. No dependencies.
- `src/PuzzleBoy.Nocturne`: finds the player's install and decodes its Unity bundles (AssetsTools.NET, Fmod5Sharp, NVorbis). It draws nothing; it uses MonoGame only for the math types its records carry.
- `src/PuzzleBoy.Rendering`: MonoGame (DesktopGL) drawing and sound for what `PuzzleBoy.Nocturne` decodes, on top of the rules.
- `tests/PuzzleBoy.*.Tests`: xunit.v3 tests on handmade stages drawn as text and made-up bytes. None needs the game.

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

To read the player's copy (`PuzzleBoy.Nocturne`):

- `GameInstall.At(folder)` takes the folder that holds `smt3hd_Data` (null when its `pazzleboy` bundle is missing); `GameInstall.Find()` searches every Steam library. Bundles are read from `smt3hd_Data/StreamingAssets/PC/<bundle>`.
- `ReadTextAssets(bundle)` returns TextAssets by name (stage files are `pazzleboy`'s). `ReadTexture` and `ReadTextures` decode Texture2Ds to `TextureData` (RGBA, Unity's bottom-up rows, every mip level). `ReadSounds(bundle)` decodes AudioClips to 16-bit PCM `SoundData`. `ReadCharacter(ramp)` reads Jack-o'-Lantern as `CharacterData`. `ReadText()` reads the font (`FontData`), its `TextMaterial`s and the English strings as `TextData`. Each throws `InvalidDataException` for data it doesn't read.

To draw and play it (`PuzzleBoy.Rendering`), at 30 ticks a second:

- `BoardView(device, atlas, background, character)` uploads `sample1` from `pazzleboy`, `BG_pzl` from `common` and the character. `Draw(board, animation, view, projection, ticks, models, viewYaw)` draws the backdrop, the board, the posed models, and the glass and goal over them.
- `CharacterModel(data)` is one Jack; `Pose(tick, x, y, yaw)` places him on a cell, fractional mid-step, before each draw.
- `MoveAnimation(board.LastMove, from)` replays a step with the original's timing: call `Tick()` each tick, read `Done`, `FallStarts`, `FallEnds` and `ArrivesAt(goal)`, and place characters with `MoveAnimation.Where`.
- `ViewCamera` is the follow camera: `TurnLeft`, `TurnRight`, `ToggleAngle`, `Quake`, `Tick`, `DirectionFor(screen)` and `View(pivot)`.
- `Sounds(clips)` takes `ReadSounds("pazzleboy")`; `Play(n)` plays effect `JYAKYO02_SE<n>` and `StartMusic()` loops the music.
- `BoardView.Upload(device, texture)` puts any `TextureData` on the GPU with its mip levels.
- `Hud(device, sheets, text)` draws the original's interface: `Draw(HudState)` for the stage counter, COMPLETE!, UNDO, scroll arrows and controls bar, `DrawMenu(Menu)` for the □ menu, `DrawHelp()` and `DrawDialogue(Dialogue)`; `TextRenderer.Bake` renders a line in the original's font.

puzzle-boy's `PuzzleBoyGame` shows the wiring: the pivot, projection, facing and which effect plays when.

The rules follow the original game's code as read in the preservation port's research (puzzle-boy's `ROADMAP.md`, "M0 findings"), with three documented differences none of the original's stages can show.

## Use it from a game

Consumers pin the engine as a git submodule at `external/puzzle-boy-engine` and reference its project:

```bash
git submodule add https://github.com/hazeliscoding/puzzle-boy-engine.git external/puzzle-boy-engine
```

```xml
<ProjectReference Include="..\..\external\puzzle-boy-engine\src\PuzzleBoy.Rules\PuzzleBoy.Rules.csproj" />
<ProjectReference Include="..\..\external\puzzle-boy-engine\src\PuzzleBoy.Nocturne\PuzzleBoy.Nocturne.csproj" />
<ProjectReference Include="..\..\external\puzzle-boy-engine\src\PuzzleBoy.Rendering\PuzzleBoy.Rendering.csproj" />
```

Reference only what you use: the rules alone need nothing else. A consumer's solution lists every engine project it builds, and its `Directory.Packages.props` needs the same package versions as this repo's.

To move a consumer to a newer engine, commit here first, then in the consumer:

```bash
git -C external/puzzle-boy-engine fetch
git -C external/puzzle-boy-engine checkout <sha>
git add external/puzzle-boy-engine
git commit -m "build: bump engine to <short-sha>"
```

List the engine commit subjects in the commit body. A consumer never points at uncommitted engine work or a floating `main`. CI checks out with `submodules: recursive`.

## Versioning

There are no version numbers, tags or packages. A consumer pins a commit SHA, and both games pin the same one: bump them together, even for a docs-only engine commit. `git submodule status` in either game shows its pin, and puzzle-boy's `ROADMAP.md` keeps the cross-repo ledger.

## Tests

```bash
dotnet build PuzzleBoy.Engine.slnx
dotnet test --solution PuzzleBoy.Engine.slnx
dotnet format PuzzleBoy.Engine.slnx --verify-no-changes
scripts/check-no-game-data.test.sh && scripts/check-no-game-data.sh
```

CI runs them on Linux, Windows and macOS. No test needs the game installed.

## Origin

The rules and their tests were copied from [hazeliscoding/puzzle-boy](https://github.com/hazeliscoding/puzzle-boy) at commit `410fc89` (the rules are unchanged at `3a06920`, where the split started). `PuzzleBoy.Nocturne` and `PuzzleBoy.Rendering` moved from puzzle-boy's `src/PuzzleBoy` at `c3b0382` (the HUD, menus and text at `83c9565`), changed only in namespace, visibility and citations. Git history was not carried over, so `git blame` here starts at the copy. The history of every rule and every drawing decision, and the research behind them, lives in puzzle-boy.

## License

Apache-2.0, see [LICENSE](LICENSE).

*Shin Megami Tensei*, *Nocturne* and *Puzzle Boy* are trademarks of ATLUS and SEGA. This is a fan project. It is not affiliated with or endorsed by either company.
