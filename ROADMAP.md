# Roadmap

What's still needed to reach feature parity with the upstream Anno 1800 editor,
plus polish items specific to this fork.

Each item is also open as a [GitHub issue](https://github.com/flamme-demon/AnnoMapEditorRenew/issues)
labelled `parity`, `enhancement` or `bug`. Anyone is welcome to grab one.

## Bug / quality

- **Replace `FileDBReader.dll` with `AnnoMods.BBDom`** (already referenced).
  `MapTemplates/Serializing/FileDBSerializer.cs` is the only consumer.
  Cleaner deserialization, fewer native deps.
- **Drop the legacy `UI/` (WPF) directory** — already excluded from the build
  via the csproj, but the files are still on disk.
- **Collision detection while dragging islands** — currently the editor lets
  islands overlap. Should highlight conflicts and snap apart.
- **Localize the modinfo.json that the exporter writes** — `ModName` and
  `Description` are duplicated identically across all 11 game languages.
- **Open issue / PR upstream** describing the `.a7t → .a7tinfo` reading fix
  so other forks benefit.

## Parity with upstream Anno 1800 editor

These features exist in the original WPF app and are not yet ported:

- **MapType selector** in the New Game UI preview (showing what the player
  will see in-game).
- **Island pool editing** — the original editor allows swapping islands
  between pools; here we only edit `IslandSize` / `IslandType`.
- **Slot assignment editor** — fertilities and mine slots can be inspected
  but not yet drag-and-drop reassigned.
- **Diff view** between vanilla template and edited mod.
- **Multi-region support** for Anno 1800 (Old World / New World / Cape /
  Arctic / Enbesa). The 117 path works; 1800 still needs verification.

## New features specific to this fork

- **Recent files / bookmarks** — startup gets slow when manually navigating
  to the same Steam Compatdata path every time.
- **Drag-out-then-drop** to add a new random island from a palette.
- **Map size resize** (currently locked to whatever the source template was).
- **Background images / heightmap preview** layered behind the canvas.
- **Mods Manager integration** — toggle mods on/off without leaving the editor.

## Tooling

- **CI**: GitHub Actions to publish a draft release with the two binaries
  (Linux, Windows .exe) on every tag.
- **macOS build** (`osx-arm64` and `osx-x64`) once someone tests it.

## Notes on Anno 117 quirks discovered

- The category in the New Game menu only appears if the mod ships **multiple
  `MapTemplate` variants** (one per difficulty) all pointing at the same
  `MapTemplateType` GUID, **and** ends `assets.xml` with
  `<ModOp Add="//MapTemplateTypes">`.
- Localized text uses `<LineId>`, **not** `<GUID>`, and the `ModOp` syntax is
  `Add="..."` not `Type="add" Path="..."`. The text for the `MapTemplateType`
  GUID is what actually shows up as the category label.
- `ModID` in `modinfo.json` only accepts lowercase letters and dashes — no
  digits, no underscores.
- The `TemplateRegion` field uses the short id (`Roman`, `Celtic`), not the
  asset display name (`Region Roman`) — spaces in paths break the engine.

These are encoded in `Mods/Serialization/Anno117ModWriter.cs`.
