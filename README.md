# AnnoMapEditorRenew

Native Linux/Windows/macOS editor for **Anno 117 — Pax Romana** map templates,
forked from [anno-mods/AnnoMapEditor](https://github.com/anno-mods/AnnoMapEditor)
and migrated from WPF to Avalonia 11.

> Not affiliated with Ubisoft. Anno 1800 and Anno 117 — Pax Romana are trademarks
> of Ubisoft Entertainment.

## What it does today (v0.8.0)

- Reads `.a7tinfo` BBDom V1/V2/V3 files (vanilla maps **and** existing mods).
- Saves bit-equivalent to vanilla DLC1 expanded templates — fixed islands,
  random islands, starting spots all serialize with the exact tag set the
  game expects (`FertilitiesPerAreaIndex`, `MineSlotActivation`,
  `FertilitySetGUIDs`, `IslandSize`, `TypePerConstructionArea`, `Rotation90`,
  explicit `<Type>` codes outside the 2020 frame).
- Renders the map: islands (with thumbnails), starting spots, NPCs, playable area.
- Two-tone overlay: live `PlayableArea` (teal) and `InitialPlayableArea`
  (green, the DLC1 2020 reference frame).
- "Vue jeu / Vue plate" toggle: rotate the canvas to the in-game diamond view
  (-45°). Persisted across sessions.
- Click-to-select, drag-to-move islands and starting spots, snap to tile grid.
- Drag-resize the playable area with 8 yellow handles.
- Edit `IslandType`, `IslandSize`, fixed-island flags (rotation / fertilities / slots).
- Duplicate, rotate, delete islands. Photoshop-style undo / redo with history list.
- Categorized tree view of all map elements (Players, Starter islands, Random,
  Fixed, NPCs, Decoration, Random NPCs, Zones).
- Island picker dialog with preview thumbnails when changing a fixed island's asset.
- Exports as a working **Anno 117 mod** (`.a7t` + `.a7te` + `.a7tinfo`,
  `assets.xml`, 12 localized text files, `modinfo.json`) — including the
  `<ModOp Add="//MapTemplateTypes">` registration that makes the new
  `MapTemplateType` show up as a category in the New Game menu.
- Re-edit any installed mod containing an `.a7tinfo` (not just the editor's own).
- CLI modes for power users: `--xml`, `--xml-decoded`, `--roundtrip-v3`
  (handy to diff your mod against vanilla `.a7tinfo`).
- French / English UI, hot-swappable from the start screen.

See [ROADMAP.md](ROADMAP.md) for what's still missing vs. the original
Anno 1800 editor.

## Install — Linux

Single self-contained binary (~95 MB). No .NET runtime to install — everything
is bundled.

```bash
chmod +x AnnoMapEditor
./AnnoMapEditor
```

To run on the Steam version of Anno 117 under Proton: point the editor at the
game install dir, typically:

```
~/.local/share/Steam/steamapps/compatdata/2980876963/pfx/drive_c/Program Files (x86)/Ubisoft/Ubisoft Game Launcher/games/Anno 117 - Pax Romana/
```

## Install — Windows

Download `AnnoMapEditor.exe` (single-file, ~99 MB) and run. No installer, no
.NET runtime to install — everything is bundled.

The installer-free build assumes a 64-bit version of Windows 10 or 11.

## Build from source

Same on both platforms.

**Prerequisites:**
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- `git`

```bash
git clone https://github.com/flamme-demon/AnnoMapEditorRenew.git
cd AnnoMapEditorRenew
dotnet run --project AnnoMapEditor/AnnoMapEditor.csproj
```

To produce release binaries:

```bash
./tools/build-release.sh
```

Output (under `build/`):

```
build/
├── linux-x64/AnnoMapEditor      (~95 MB)
└── win-x64/AnnoMapEditor.exe    (~99 MB)
```

## Status

Early fork — works end-to-end on Anno 117 (creating mods that show up in the
game's New Game menu) but does not yet have full feature parity with the
upstream Anno 1800 editor. See [ROADMAP.md](ROADMAP.md) and the
[issue tracker](https://github.com/flamme-demon/AnnoMapEditorRenew/issues)
if you want to help.

## License

MIT, same as the upstream project.
