# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

WPF desktop app (.NET 10, `net10.0-windows`, Windows only) to tail log files on remote SMB shares. It replaces `New-SmbMapping` + `Get-Content -Tail N -Wait`. The UI text is in Italian. There are no external NuGet dependencies in the app; DPAPI (`ProtectedData`) is included with the WindowsDesktop framework.

## Commands

```powershell
dotnet build
dotnet test
dotnet test --filter "FullyQualifiedName~LogTailerTests"            # one class
dotnet test --filter "FullyQualifiedName~LogTailerTests.ReadTail_returns_last_n_lines"   # one test
dotnet run --project src/RemoteLogViewer
```

- **NuGet:** the repo-local `nuget.config` clears sources and source mappings and uses only nuget.org, under the key `nuget-public`. It exists because the machine's global NuGet config disables `nuget.org` and points to a private feed that returns 401. Don't delete it.
- **Build fails with a locked exe:** a running `RemoteLogViewer.exe` locks the build output. Kill it first with `taskkill //IM RemoteLogViewer.exe //F`.

## Architecture

The data flow crosses threads, so it only makes sense when you read several files together:

1. **`ActiveSession`** runs one background `Task` loop per open session:
   - It connects the share (`SmbConnection`), calls `LogTailer.ReadTail(N)` once, then `Poll()` every 500 ms.
   - It pushes `LogLine`s (level detected via `LogLevels.Detect`, which inherits the previous level for stack-trace lines) into a `ConcurrentQueue` called `Pending`.
   - It raises `StateChanged` on the background thread.
   - **State machine:**
     - A failure on the first connect or read goes to `Error`, with no retry. This means wrong credentials or a wrong path.
     - Once a read has succeeded, an I/O error goes to `Reconnecting`, which retries every 5 s and resumes from the tailer's offset.
     - A missing file goes to `Waiting`.
   - It runs on a **clone** of the saved `SessionConfig`, so edits to saved sessions don't affect running ones.
   - **Date paths:** `FilePath` may contain `{date:<.NET format>}` (`DatePath` resolves/validates it). `SessionConfig.FullPath` is the *display* path with placeholders unresolved; the path actually opened is `ActiveSession.Path` (= `ResolvePath(Date)`). `MainWindow.Open` asks the date via `DateDialog`.
   - If the chosen date was today (`FollowToday`), the loop switches to the new day's file when the injectable clock (`today` ctor arg, for tests) changes: marker line, new tailer read from the start (`ReadTail(100_000)`), `StateChanged` so the tab title (`DisplayName`) updates.
2. **`LogView`** (one per session) drains `Pending` on a 200 ms `DispatcherTimer`:
   - It keeps the full buffer in `_all` (a `List`) and the filtered view in `_visible` (an `ObservableCollection` bound to a virtualized `ListBox`).
   - Changing the filter, or trimming past 100k lines, **rebuilds** `_visible` rather than mutating it.
   - Follow mode is driven by `ScrollChanged`: a user scroll-up turns Follow off, and reaching the bottom turns it back on.
3. **`LogTailer`** opens a short-lived `FileStream` per call, with `FileShare.ReadWrite|Delete`, so it never blocks server-side log rotation:
   - A shrinking length or a changed creation time counts as rotation, and reading restarts from 0.
   - A stateful `Decoder` plus a `_partial` buffer keep multibyte chars and incomplete lines intact across polls.
   - A BOM overrides the configured encoding.
4. **`SmbConnection`** is a P/Invoke wrapper around `WNetAddConnection2`/`WNetCancelConnection2`, with no local drive name:
   - Connections are ref-counted per share across sessions.
   - A connection is only cancelled if the app opened it; `ERROR_ALREADY_ASSIGNED` (85) means the user's existing mapping, which is left alone.
   - Errors become `SmbException` with Italian messages.
   - Error 1219 (same server, different credentials) is a Windows limitation, not a bug.
5. **`SessionStore`** reads and writes `%APPDATA%\RemoteLogViewer\sessions.json` using camelCase JSON:
   - Writes are atomic (a temp file, then a move).
   - Passwords are stored as DPAPI CurrentUser base64. `Unprotect` returns null when a password can't be decrypted.
   - A corrupt file is moved to `.bak`.
   - `Load()` returns a **`SessionTree`**: flat lists of `SessionCollection { Id, Name, ParentId }` and sessions with `CollectionId` (null = root). The file stays v1-compatible (missing `collections` → everything at root); `Normalize()` repairs orphans/cycles.
   - `SessionTree` holds all tree logic (move with cycle check, recursive delete/count, `UniqueName` "X (2)", export/import, `Duplicate` = export+import with `keepPasswords`). Export uses the same JSON shape plus `format`/`version`, **never** passwords; import validates everything first (all-or-nothing) and remaps every id to a new Guid.
6. **`MainWindow`**:
   - Sidebar is a `TreeView` of `CollectionNode`/`SessionNode` **rebuilt from `SessionTree` after every change** (`RebuildTree(select)`); expansion state lives in `_expanded`, selection is restored by object. The context menu is built on `ContextMenuOpening` from the node under the mouse (empty space = root actions).
   - The `TabItem`s are the source of truth, and each one's `Tag` holds its `LogView`. `Relayout()` detaches every `LogView` and re-parents it into either the tabs or the `UniformGrid` for "Affianca" (side-by-side), in tab order. Call it after any add, close, toggle or tab drag. Tab drag moves the `TabItem` only; the `LogView`/session is untouched.

## Gotchas

- **`LogLine` must stay a class, not a record.** Value equality would make identical log lines collide in `ListBox` selection. `IsMatch` raises `INotifyPropertyChanged` for search highlighting.
- **Use `Checked`/`Unchecked` for toggles and checkboxes, not `Click`.** `Click` doesn't fire for UI Automation or programmatic toggles. `Checked` fires during `InitializeComponent` when `IsChecked="True"` is set in XAML, so handlers must guard with `IsLoaded`.
- **`ActiveSessionTests` does real SMB I/O through `\\localhost\C$`, falling back to `\\127.0.0.1\C$`** (on some machines only the IP works) as the current user. It returns early and passes silently if neither is reachable. Claude Code's sandbox blocks SMB: run these tests outside the sandbox, or they silently skip.
- **Menu items with dynamic names use a `TextBlock` header** so `_` in names (e.g. `DOB_SDL`) isn't eaten as an access key; they set `AutomationProperties.Name` explicitly, otherwise UI Automation/screen readers see no name.
- **Drag start for tabs is detected on the `TabControl`'s `PreviewMouseMove`**, not on the `TabItem`: a fast drag leaves the small header before the first move event.
- **`Environment.GetFolderPath(ApplicationData)` ignores the `APPDATA` env var.** Manual UI testing therefore reads and writes the real `sessions.json`. Clean it up afterwards.

## Workflow for code changes (mandatory)

1. **Ask first.** Before any request that needs code changes, ask the user whether to use spec-kit or not. Don't assume either way.
2. **If spec-kit is used**, the branch depends on where you are:
   - **Not on a `feature/*` branch** (e.g. `develop`): before implementing, create `feature/<name>`, where `<name>` is the feature name chosen by spec-kit (e.g. `feature/001-remote-log-viewer`).
   - **Already on a `feature/*` branch**: stay on it and don't create a new branch. The spec-kit feature name must be rooted in the current feature: `<current-feature>-<short-name>`. For example, on `feature/001-remote-log-viewer` it becomes `specs/001-remote-log-viewer-<short-name>/`, passed as `SPECIFY_FEATURE_DIRECTORY`.
3. **After every change**, update `README.md` and this `CLAUDE.md` if the change affects what they describe: features, commands, architecture, gotchas.

## Spec-kit workflow

- Features are specified with the spec-kit skills (`/speckit-specify` → `plan` → `tasks` → `implement`) under `specs/`.
- `.specify/feature.json` (the active feature pointer) is gitignored.
- The Python scripts in `.specify/scripts/python/` need `PYTHONIOENCODING=utf-8` on this machine. Without it, output with non-ASCII characters crashes on cp1252.
- `.specify/memory/constitution.md` is still the unfilled template.
