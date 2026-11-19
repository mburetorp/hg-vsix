# Hg-VSIX - Mercurial visualizer for Visual Studio 2026

A small, read-only Visual Studio extension that shows uncommitted Mercurial changes.

- **Change margin**: green (added), blue (modified) and red wedges (deleted) next to the editor, including unsaved edits.
- **Compare with Mercurial Parent**: opens the built-in VS diff window with the parent revision on the left and the open document (with unsaved edits) on the right. Available from the editor context menu and the Tools menu for files inside an hg repository.
- **Automatic refresh**: follows edits, external `hg commit`, `hg update`, `hg add`, `hg revert` and renames.

Committing, pushing, pulling, merging and every other source control operation stays outside Visual Studio. The extension only runs `hg log`, `hg status` and `hg cat`.

## Requirements

- Visual Studio 2026 (Community, Professional or Enterprise)
- Mercurial (`hg.exe`), for example from TortoiseHg

## Install

1. Download `HgVsix.vsix` from the latest GitHub release.
2. Close Visual Studio.
3. Double-click `HgVsix.vsix` and pick the Visual Studio 2026 edition(s) to install into.

Newer releases install over older ones the same way. Uninstall from Extensions > Manage Extensions.

## Settings

Tools > Options > Hg-VSIX > General

| Setting | Default | Meaning |
|---|---|---|
| hg executable | empty | Full path to `hg.exe`. Empty means `hg` on PATH, then `C:\Program Files\TortoiseHg\hg.exe`. |
| Enabled | true | Turns everything on or off. |
| Show change margin | true | Shows or hides the margin. |
| Maximum file size (KB) | 5120 | Larger files are not compared. |
| Verbose logging | false | Logs every hg command to the Output window (pane "Hg-VSIX"). |

Marker colors: Tools > Options > Environment > Fonts and Colors > Text Editor > "Hg-VSIX Added Line", "Hg-VSIX Modified Line" and "Hg-VSIX Deleted Line" (item background).

Errors from hg are written to the Output window pane "Hg-VSIX". A failing hg never blocks the editor. The file just shows no markers.

## How it works

- The repository is found by walking up to the nearest `.hg` folder. No process is started for files outside repositories.
- Mercurial subrepos are supported. A file belongs to the nearest repository, so files in a subrepo compare against the subrepo working directory parent, like `hg -R <subrepo> diff`. `--subrepos` is never used.
- The baseline is the first parent of the working directory (`hg cat -r .`). Renamed or copied files use their copy source. Added files show every line as added. Untracked, ignored and binary files show nothing.
- Lines are compared with the VS differencing service after normalizing line endings, so CRLF/LF differences do not show up as changes.
- `.hg\dirstate` is watched for changes. When Visual Studio regains focus the dirstate is checked again, in case the watcher missed something.
- The parent revision is cached until the dirstate changes. File status is always read fresh because editing or restoring a working file does not touch the dirstate.
- During a merge the first parent is used.

## Build

Requires Visual Studio 2026 with the "Visual Studio extension development" workload.

| Script | What it does |
|---|---|
| `build.bat [Release\|Debug]` | Builds the solution with Visual Studio 2026 MSBuild. Default Release. |
| `build-and-install.bat` | Builds Release and opens the Visual Studio extension installer, where you pick the editions. |

The installer refuses a package with the same version as the installed one. To reinstall a local build without raising the version, uninstall it first in Extensions > Manage Extensions and restart Visual Studio.

All build output goes to `build\` at the repo root; delete it for a clean build. The package is written to `build\bin\HgVsix\<config>\net472\HgVsix.vsix`.

Debug: open `HgVsix.sln`, set `HgVsix` as startup project and press F5. This starts the experimental instance (`devenv /rootsuffix Exp`).

## Release

1. Raise `Version` in `src/HgVsix/source.extension.vsixmanifest`. Visual Studio only upgrades to a higher version.
2. Run `build.bat` and the tests.
3. Commit, tag and publish the package:

```
git tag v1.0.0
git push origin main v1.0.0
gh release create v1.0.0 build\bin\HgVsix\Release\net472\HgVsix.vsix --title "Hg-VSIX 1.0.0" --notes "..."
```

## Tests

```
dotnet test test\HgVsix.Core.Tests
```

The tests cover the hg layer (against real temporary repositories when `hg` is available), line classification and text decoding. Editor behavior is tested manually with [docs/manual-test-scenarios.md](docs/manual-test-scenarios.md).

## Layout

| Path | Content |
|---|---|
| `src/HgVsix.Core` | hg process runner, repository locator, baseline provider, change model. No VS references. |
| `src/HgVsix` | VSIX: margin, compare command, refresh coordinator, options. |
| `test/HgVsix.Core.Tests` | xUnit tests for Core. |

## Known limitations

- Only the first parent is used during merges.
- Untracked files show no markers.
- No hover preview, per hunk diff or change navigation yet.
- Files in projection buffers (for example Razor) are mapped to the underlying document where possible.

## License

[MIT](LICENSE)
