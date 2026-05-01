# AnnoMapEditorRenew

Native Linux/Windows/macOS editor for **Anno 117 — Pax Romana** map templates,
forked from [anno-mods/AnnoMapEditor](https://github.com/anno-mods/AnnoMapEditor)
and migrated from WPF to Avalonia 11.

> Not affiliated with Ubisoft. Anno 1800 and Anno 117 — Pax Romana are trademarks
> of Ubisoft Entertainment.

## What it does today (v0.7.0-fork.1)

- Reads `.a7tinfo` BBDom V1/V2/V3 files (vanilla maps **and** existing mods).
- Renders the map: islands (with thumbnails), starting spots, NPCs, playable area.
- Click-to-select, drag-to-move islands and starting spots, snap to tile grid.
- Edit `IslandType`, `IslandSize`, fixed-island flags (rotation / fertilities / slots).
- Duplicate, rotate, delete islands. Photoshop-style undo / redo with history list.
- Categorized tree view of all map elements (Players, Starter islands, Random,
  Fixed, NPCs, Decoration, Random NPCs).
- Exports as a working **Anno 117 mod** (`.a7t` + `.a7te` + `.a7tinfo`,
  `assets.xml`, 12 localized text files, `modinfo.json`) — including the
  `<ModOp Add="//MapTemplateTypes">` registration that makes the new
  `MapTemplateType` show up as a category in the New Game menu.
- Re-edit any installed mod containing an `.a7tinfo` (not just the editor's own).
- French / English UI, hot-swappable from the start screen.

See [ROADMAP.md](ROADMAP.md) for what's still missing vs. the original
Anno 1800 editor.

## Install — Linux

Two options.

### AppImage (recommended)

Self-contained, no .NET install needed.

```bash
chmod +x AnnoMapEditor-0.7.0-fork.1-x86_64.AppImage
./AnnoMapEditor-0.7.0-fork.1-x86_64.AppImage
```

**Required system packages:**
- `fuse2` (or `libfuse2`) — to launch the AppImage. On Manjaro/Arch:
  `sudo pacman -S fuse2`. On Ubuntu/Debian: `sudo apt install libfuse2`.

To run on the Steam version of Anno 117 under Proton: point the editor at the
game install dir, typically:

```
~/.local/share/Steam/steamapps/compatdata/2980876963/pfx/drive_c/Program Files (x86)/Ubisoft/Ubisoft Game Launcher/games/Anno 117 - Pax Romana/
```

### Plain executable

If AppImage doesn't run on your distro:

```bash
./AnnoMapEditor          # in publish/linux-x64/ from a release zip
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
./tools/build-release.sh        # publishes Linux + Windows self-contained
./tools/build-appimage.sh       # wraps the Linux build into an AppImage
```

`tools/build-appimage.sh` downloads `appimagetool` automatically on first run.

## Status

Early fork — works end-to-end on Anno 117 (creating mods that show up in the
game's New Game menu) but does not yet have full feature parity with the
upstream Anno 1800 editor. See [ROADMAP.md](ROADMAP.md) and the
[issue tracker](https://github.com/flamme-demon/AnnoMapEditorRenew/issues)
if you want to help.

## License

MIT, same as the upstream project.
