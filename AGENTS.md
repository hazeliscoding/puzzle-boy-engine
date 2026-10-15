# AGENTS.md

These are the working rules for agents in this repo. Puzzle Boy Engine is the headless rules of the Puzzle Boy minigame from *Shin Megami Tensei III Nocturne HD Remaster*, plus the libraries that read the player's own copy and draw it the original's way. The preservation port (puzzle-boy) and Community Edition (puzzle-boy-community) share it. Licensed Apache-2.0.

## Sources of truth

- `README.md`: what the engine is, its API and how consumers pin it.
- `ROADMAP.md`: decisions already made, the milestones, and what is out of scope. Work from the next unchecked item and record new decisions there. Don't build past the current milestone without asking.
- The research behind the rules and the classic presentation lives in puzzle-boy's `ROADMAP.md`, not here. Citations of `re/m3-*.md` in `PuzzleBoy.Nocturne` and `PuzzleBoy.Rendering` name puzzle-boy's local research notes, which no repo holds.

## Hard rules

- **No Atlus data in the repo.** That covers stage files, text, audio, textures, models, fonts, asset bundles, metadata dumps, method address tables and decompiler output, in every project, `PuzzleBoy.Nocturne` and `PuzzleBoy.Rendering` included. They read the player's install at runtime; nothing is embedded, bundled or downloaded. Their tests use handmade bytes, meshes and fonts only and never need the game.
- **No decompiled code in the source.** A rule comes from a test on a handmade stage. Don't paste or transliterate pseudocode.
- **No secrets or private paths** in committed files.
- **The public API is a contract with two games.** Change it on purpose: small, documented as a decision in `ROADMAP.md`, with regression tests written first. Don't change the meaning of `Board`, `Stage.Parse` or `Cell` without a test that shows the old and new behavior.

## Commands

- `dotnet build PuzzleBoy.Engine.slnx` builds everything. Warnings are errors.
- `dotnet test --solution PuzzleBoy.Engine.slnx` runs the tests. They use handmade stages only.
- `dotnet format PuzzleBoy.Engine.slnx --verify-no-changes` checks the style CI will enforce.
- `scripts/check-no-game-data.sh` fails if tracked files hold Atlus data or decompiler output. `scripts/check-no-game-data.test.sh` is its positive control: it plants each kind in a scratch repo and expects a failure. When you add a new kind, add both a check and a planted case.

## Code layout

- `src/PuzzleBoy.Rules`: stage parsing (`Stage`, `Cell`) and the rules (`Board`). No MonoGame, no file I/O and no dependencies, so the tests can drive it with plain bytes. Keep it that way.
- `src/PuzzleBoy.Nocturne`: `GameInstall` finds the install and reads its bundles with AssetsTools.NET; `UnityFormats` and `TextureDecoder` decode meshes, animation and textures; the records (`TextureData`, `SoundData`, `CharacterData`, `TextData`, …) carry the result. It uses MonoGame for math types only.
- `src/PuzzleBoy.Rendering`: MonoGame drawing and sound: `BoardView`, `CharacterView`, `CharacterModel`, `ViewCamera`, `MoveAnimation`, `Sounds`, and the original's interface: `Hud`, `TextRenderer`, `Menu`, `Dialogue`. Its look and timing are the preservation port's; change them only with evidence recorded in puzzle-boy's `ROADMAP.md`.
- `tests/PuzzleBoy.Rules.Tests`: xunit.v3 on Microsoft Testing Platform. Rules tests draw handmade stages as text with `Ascii.cs`; its summary has the legend. A rule change starts with a drawing of the case. Test names read as sentences.
- `tests/PuzzleBoy.Nocturne.Tests` and `tests/PuzzleBoy.Rendering.Tests`: the same, on made-up bytes and models. The rendering tests share `Ascii.cs` by link.

## Consumers

- puzzle-boy and puzzle-boy-community pin this repo as a submodule at `external/puzzle-boy-engine`. After an engine commit they need to bump their pin (`build: bump engine to <short-sha>`); README.md has the steps.
- What belongs to one game stays there: the original 20-file order and when the dialogue plays in puzzle-boy; the bonus order, stage catalogs, themes, classic packs and menus in Community; input and stage flow in each game. Bundle reading and the classic board, character, HUD, menu and text drawing are shared here.

## Working style

- **Commits:** [Conventional Commits](https://www.conventionalcommits.org/) (`feat:`, `fix:`, `docs:`, `chore:`, `test:`, `ci:`, `build:`, `refactor:`). Keep each commit atomic, and use a scope when it adds clarity (`feat(engine): …`).
- **No AI attribution** in commits or PRs. That means no `Co-Authored-By` trailers, no "Generated with" lines and no session links.
- **Checks:** automate acceptance checks instead of handing manual steps to the owner. Every check that tests for an absence needs a positive control.
- **Docs:** short and concise. Prefer editing `ROADMAP.md` over creating new planning documents.
- **Documentation:** every public type and member has an XML doc comment that says what it's for and any contract a caller can't see from the signature: units, ranges, what's null, what throws. Overrides use `<inheritdoc />`. The build fails on a missing one (CS1591); tests are exempt.
- **Comments in code:** explain why, not what: a constraint, a quirk of the original game, a workaround and its cause. Mark a known limit as `shortcut: <the limit>, <when to upgrade>`.
- **Pragmatic code:** the smallest change that fully works. No abstractions, options or layers for later, and nothing the current roadmap item doesn't need.
