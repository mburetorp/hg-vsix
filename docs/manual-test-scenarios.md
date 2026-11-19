# Manual test scenarios

Run in the experimental instance (F5 from VS 2026 Professional) and once more after installing the `.vsix` into VS 2026 Community.

## Setup

Create a scratch repository from a command prompt:

```
mkdir C:\temp\hgvsix-scratch
cd /d C:\temp\hgvsix-scratch
hg init
(echo line 1& echo line 2& echo line 3& echo line 4& echo line 5) > a.txt
copy a.txt b.cs
hg commit -A -m "initial" -u test
```

Open the folder in VS (File > Open > Folder) and open `a.txt`.
Keep the Output window pane "Hg-VSIX" visible and turn on Verbose logging in Tools > Options > Hg-VSIX.

## Scenarios

The Covers column names the acceptance criterion (AC) or edge case each row checks:

| AC | Criterion |
|---|---|
| AC1 | Opening a modified tracked file shows the correct markers |
| AC2 | Typing, deleting or changing lines updates markers without saving |
| AC3 | Compare shows the committed version next to the current editor contents |
| AC4 | An external `hg commit` clears the markers |
| AC5 | An external `hg update` switches to the new parent |
| AC6 | Scrolling, resizing and folding keep markers aligned |
| AC7 | Files outside Mercurial repositories work normally |
| AC8 | Git integration is not affected |
| AC9 | hg failures never crash or block the editor |

| # | Steps | Expected | Covers |
|---|---|---|---|
| 1 | Open the clean `a.txt` | Margin visible but empty | Clean tracked file |
| 2 | Edit `a.txt` outside VS (change line 2), switch back to VS | Blue marker on line 2 | AC1, modified file |
| 3 | In VS type a new line after line 3 without saving | Green marker on the new line within about half a second | AC2, unsaved changes |
| 4 | Delete line 5 in VS | Red wedge at the boundary where line 5 was | AC2, deleted lines |
| 5 | Undo all edits (Ctrl+Z) | Markers return to the state on disk | AC2 |
| 6 | Right-click in the editor > Compare with Mercurial Parent | VS diff window: left "a.txt @ <node> (parent)", right "a.txt (working copy)" including unsaved edits | AC3 |
| 7 | Tools > Compare with Mercurial Parent | Same as 6 | AC3 |
| 8 | Run `hg commit -m x -u test` in the scratch folder, switch to VS | Markers disappear (only unsaved edits remain marked) | AC4, external commit |
| 9 | Run `hg update -r 0`, switch to VS | VS reloads the file. Markers compare against revision 0 | AC5, external update |
| 10 | Scroll, zoom (Ctrl+mouse wheel), resize the window, turn on word wrap | Markers stay aligned with their lines. Wrapped lines are marked on every row. | AC6 |
| 11 | Collapse a region containing changes (on `b.cs`, add a method with changes and collapse it) | The collapsed line carries the marker | AC6, collapsed regions |
| 12 | Open a file outside any hg repository | No margin, no context menu command, no hg processes in the log | AC7 |
| 13 | Open a file in a git repository | No margin. Git features unaffected. | AC8 |
| 14 | Set "hg executable" to a missing path, reopen a file | No markers, an error line in the "Hg-VSIX" pane, editor responsive. Restore the setting afterwards. | AC9 |
| 15 | Create `new.txt`, run `hg add new.txt`, open it | Every line marked as added | Newly added file |
| 16 | Create `loose.txt` without adding it | No markers. Compare shows "not tracked". | Untracked file |
| 17 | `hg mv a.txt renamed.txt`, open `renamed.txt`, change a line | Only the changed line is marked. Compare shows `a.txt @ <node>` on the left. | Renamed file |
| 18 | Open a second repository in the same session and edit files in both | Each file compares against its own repository | Multiple repositories |
| 19 | Commit a binary file (for example a .png) and open it as text | No markers. Compare reports binary. | Binary files |
| 20 | Open a file larger than the configured limit | No markers, typing is not slowed down | Large files |
| 21 | Convert `a.txt` to CRLF line endings outside VS without other changes | No markers | EOL normalization |
| 22 | Save `a.txt` as UTF-8 with BOM (File > Save As > Save with Encoding) without other changes | No markers | Encoding |
| 23 | Start a merge (`hg merge` with a conflict-free second head), open a file | Markers compare against the first parent | Merge in progress |
| 24 | Open the same file in two windows (Window > New Window) and edit | Both margins update together | Shared buffer |
| 25 | Change the marker colors in Fonts and Colors | Margin uses the new colors after OK | Configurable colors |
| 26 | Turn off "Show change margin" | Margin disappears in all open editors. Compare still works. | Settings |
