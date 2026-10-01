# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

WPF desktop app (.NET 10, `net10.0-windows`, Windows only) to tail log files on remote SMB shares (and local files opened from the file system). It replaces `New-SmbMapping` + `Get-Content -Tail N -Wait`. The UI is localized (it/en, see Localization below); Italian is the source language. There are no external NuGet dependencies in the app; DPAPI (`ProtectedData`) is included with the WindowsDesktop framework.

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
   - It pushes `LogLine`s (built by `LogLevels.Make`: level via `LogLevels.Detect`, which inherits the previous level for stack-trace lines) into a `ConcurrentQueue` called `Pending`. Flags: `IsEntry` = level detected on that line (a stack trace is one entry: counters/badges count entries), `IsHistory` = from the initial `ReadTail` (no alerts; the new-day read-from-start is *not* history).
   - **Load previous lines:** the UI calls `RequestOlder(n)` (atomic int); the loop, which owns the tailer, runs `ReadBefore(n)` on its next iteration and enqueues the block in `Older` (`ConcurrentQueue<List<LogLine>>`). `CanLoadOlder` (Running && `tailer.HasOlder`) raises `StateChanged` when it changes.
   - It raises `StateChanged` on the background thread.
   - **State machine:**
     - A failure on the first connect or read goes to `Error`, with no retry. This means wrong credentials or a wrong path.
     - Once a read has succeeded, an I/O error goes to `Reconnecting`, which retries every 5 s and resumes from the tailer's offset.
     - A missing file goes to `Waiting`.
   - It runs on a **clone** of the saved `SessionConfig`, so edits to saved sessions don't affect running ones.
   - **Date paths:** `FilePath` may contain `{date:<.NET format>}` (`DatePath` resolves/validates it). `SessionConfig.FullPath` is the *display* path with placeholders unresolved; the path actually opened is `ActiveSession.Path` (= `ResolvePath(Date)`). `MainWindow.Open` asks the date via `DateDialog` (gets the config with credential applied): an inline `Calendar` that connects the share once (released on close) and `File.Exists`-probes every day of each displayed month, adding missing days to `BlackoutDates` (never today or the selected date: blacking out the selected date throws).
   - `SessionConfig.CurrentUndated` (checkbox *Il file di oggi non ha la data*): `ResolvePath(date, today)` strips every placeholder (`DatePath.Strip`) when `date` is today, so `app{date:'.'yyyy-MM-dd}.log` → `app.log` today, dated otherwise (separator goes inside the format, quoted). `ActiveSession.Path` passes its injectable clock.
   - If the chosen date was today (`FollowToday`), the loop switches to the new day's file when the injectable clock (`today` ctor arg, for tests) changes: marker line, new tailer read from the start (`ReadTail(100_000)`), `StateChanged` so the tab title (`DisplayName`) updates. With `CurrentUndated` the path doesn't change, so only `Date`/marker update and the tailer is kept (its rotation detection handles the server's rename).
2. **`LogView`** (one per session) drains `Pending` on a 200 ms `DispatcherTimer`:
   - It keeps the full buffer in `_all` (a `List`) and the filtered view in `_visible` (an `ObservableCollection` bound to a virtualized `ListBox`).
   - Filter = level checkboxes (`_hiddenLevels`; `None` and marker lines are never hidden) AND text/regex, with optional `grep -C N` context. All of it lives in **`ContextFilter`** (pure, tested), fed one buffer index at a time: `Drain` feeds new lines, `Rebuild` = new `ContextFilter` + replay of `_all`. It is index-based, so anything that shifts `_all` (trim past 100k, prepend of `Older`) must `Rebuild`. Context separators (`--`, marker `LogLine`s) exist only in `_visible`; `IsContext` dims context lines.
   - Prepend keeps the view still: `ListBox` scrolls by item, so `Rebuild(keepTopLine)` re-scrolls to the old top item's new index.
   - `_counts` (entries per level, shown in the level checkboxes) is updated incrementally, fully recounted after a trim and zeroed by *Pulisci*.
   - Alerts: `UnreadErrors` grows only while `!IsVisible` (a non-selected tab's content is out of the visual tree; in Affianca everything is visible) and resets on `IsVisibleChanged`. `ErrorsArrived` → `MainWindow` flashes the taskbar (`FlashWindowEx`) when the window is not active.
   - **Cross-tab search:** `Snapshot()` (copy of `_all`, UI thread only) feeds `LogSearch.Run`; `Reveal(LogLine)` selects a hit by object identity (`NotFound` if trimmed/cleared) and, if the level/text filters hide it, clears them and rebuilds explicitly (the `IsLoaded` guards would skip it on a not-yet-shown tab).
   - `Ctrl+S` = `ApplicationCommands.Save` bound on the `LogView` (saves `_visible` texts, UTF-8 without BOM).
   - Follow mode is driven by `ScrollChanged`: a user scroll-up turns Follow off, and reaching the bottom turns it back on.
3. **`LogTailer`** opens a short-lived `FileStream` per call, with `FileShare.ReadWrite|Delete`, so it never blocks server-side log rotation:
   - A shrinking length or a changed creation time counts as rotation, and reading restarts from 0.
   - `ReadTail`/`ReadBefore` scan **bytes** backward (256 KB blocks) for newlines (`0A`; UTF-16 LE `0A 00` / BE `00 0A` on 2-byte boundaries from the BOM), so `_head` (offset of the oldest line handed out) is exact; they decode only whole lines. `HasOlder = _head > _start`; rotation sets `_head = _start`.
   - A stateful `Decoder` plus a `_partial` buffer keep multibyte chars and incomplete lines intact across polls.
   - A BOM overrides the configured encoding.
4. **`SmbConnection`** is a P/Invoke wrapper around `WNetAddConnection2`/`WNetCancelConnection2`, with no local drive name:
   - Connections are ref-counted per share across sessions.
   - A connection is only cancelled if the app opened it; `ERROR_ALREADY_ASSIGNED` (85) means the user's existing mapping, which is left alone.
   - Errors become `SmbException` with localized messages (`L.T`).
   - Error 1219 (same server, different credentials) is a Windows limitation, not a bug.
5. **`SessionStore`** reads and writes `%APPDATA%\RemoteLogViewer\sessions.json` using camelCase JSON:
   - Writes are atomic (a temp file, then a move).
   - Passwords are stored as DPAPI CurrentUser base64. `Unprotect` returns null when a password can't be decrypted.
   - **Credentials:** `SessionTree.Credentials` (`Credential { Id, Name, UserName, ProtectedPassword }`, managed in `CredentialsWindow`). A session with `CredentialId` ignores its inline `UserName`/`ProtectedPassword`; `SessionConfig.WithCredential(creds)` returns the clone with the credential applied and is what `MainWindow.Open` and the edit window's connection test use. `DeleteCredential` and `Normalize` null dangling `CredentialId`s. Credentials are never exported; import keeps a `CredentialId` only if it exists locally.
   - A corrupt file is moved to `.bak`.
   - `Load()` returns a **`SessionTree`**: flat lists of `SessionCollection { Id, Name, ParentId }` and sessions with `CollectionId` (null = root). The file stays v1-compatible (missing `collections` → everything at root); `Normalize()` repairs orphans/cycles.
   - `SessionTree` holds all tree logic (move with cycle check, recursive delete/count, `UniqueName` "X (2)", export/import, `Duplicate` = export+import with `keepPasswords`). Export uses the same JSON shape plus `format`/`version`, **never** passwords; import validates everything first (all-or-nothing) and remaps every id to a new Guid.
6. **`MainWindow`**:
   - Sidebar is a `TreeView` of `CollectionNode`/`SessionNode` **rebuilt from `SessionTree` after every change** (`RebuildTree(select)`); expansion state lives in `_expanded`, selection is restored by object. The context menu is built on `ContextMenuOpening` from the node under the mouse (empty space = root actions).
   - **Workspace:** `Window_Closing` stores open tabs (`OpenTab { SessionId, Date, FollowToday }`), selected index and Affianca into `SessionTree.Workspace` (`workspace` in `sessions.json`, never exported) and saves; `Window_Loaded` reopens them via `Open(config, date)` (skips `DateDialog`; `FollowToday` → today; missing sessions skipped).
   - **Local files:** *Apri file…* (`ApplicationCommands.Open`, `Ctrl+O`, `OpenFileDialog`) and drag & drop (`Window_PreviewDragOver`/`PreviewDrop`, only for `FileDrop`: the tab/tree handlers would reject it, internal drags use `rlv-tab`/`rlv-tree-item`) go through `OpenFiles`: non-files ignored, an already open path (case-insensitive) is just selected, a file that cannot be opened (probe with `FileShare.ReadWrite|Delete`) gives a message and no tab. `SessionConfig.Local(path)` (`IsLocal`, `FullPath`/`ResolvePath` = `FilePath`, 1000 initial lines, auto encoding) is never stored in `SessionTree.Sessions`; `ActiveSession` skips `SmbConnection.Connect/Release` for it, so state machine, rotation and wait-for-file are the remote ones. `LocalConfig` titles a duplicate name as `folder\name`. The workspace stores `OpenTab.LocalPath` (`SessionId` empty); `Window_Loaded` reopens it if the file exists, silently skips it otherwise.
   - **Advanced search:** `Search` `RoutedCommand` (toolbar *Cerca…*, `Ctrl+Shift+F`) opens the singleton non-modal `SearchWindow` (`_search`, owner = main window). It snapshots every `LogView` (`Tabs` `Tag`s), runs `LogSearch.Run` in `Task.Run` (cancellable; a new search cancels the previous one) and shows a flat virtualized list of `ResultRow`s (group headers + hits, max `LogSearch.MaxHits` = 10,000). `LogSearch` and `LogTimestamp` are pure and tested: everything is a `Regex` (plain text escaped, whole word = `(?<!\w)…(?!\w)`), levels with a partial filter exclude `LogLevel.None`, the time range uses the line's timestamp (ISO / `dd/MM/yyyy` / time only → `SearchSource.DefaultDate`, i.e. `Session.Date ?? today`) and lines without one inherit the previous one per session. Double-click → `MainWindow.RevealLine` (selects the tab unless Affianca, then `LogView.Reveal`). *Esporta…* = `LogSearch.FormatExport`.
   - **Help:** `F1`/*Guida* = `ApplicationCommands.Help`: the embedded resource for the current language (`Guida.it.html` / `Guida.en.html`, `LogicalName` `RemoteLogViewer.Guida.<lang>.html`, with `WithCulture="false"` or MSBuild treats `.it.` as a satellite culture) is written to `%TEMP%\RemoteLogViewer\guide.<lang>.html` with `%VERSION%` replaced by the assembly informational version, then opened with the shell.
   - The `TabItem`s are the source of truth, and each one's `Tag` holds its `LogView`. `Relayout()` detaches every `LogView` and re-parents it into either the tabs or the `UniformGrid` for "Affianca" (side-by-side), in tab order. Call it after any add, close, toggle or tab drag. Tab drag moves the `TabItem` only; the `LogView`/session is untouched.

## Localization

- **`L`** (`L.cs`): the Italian text is the key, the English one lives in the `En` table. `L.T("it")` / `L.F("it {0}", args)` in code, `{local:T 'it'}` markup extension in XAML (inside the quotes escape `\` as `\\` and `'` as `\'`). A missing entry silently falls back to Italian, so add every new string to `En`.
- `L.Lang` defaults to `"it"` (tests assert Italian messages); `App()` calls `L.Init()`: `%APPDATA%\RemoteLogViewer\lang.txt`, else the OS UI language (it, otherwise en). It also sets the culture; `DateDialog` sets its WPF `Language` (calendar month names). Switching language (toolbar `LangBox` → `L.SetLang`) is live: it writes `lang.txt`, bumps `LocSource.Version` (every `{local:T}` / `L.Bind` is a binding on it, so XAML text refreshes) and raises `L.Changed` for text set from code (`LogView` refreshes status/counters; `ActiveSession.StatusText` is translated on read). Code that sets localized text once from code on a long-lived control must use `L.Bind` or subscribe to `L.Changed` (and unsubscribe on close).
- Stored data that embeds localized text (e.g. the `(copia)`/`(copy)` suffix of duplicated names) is written in the language active at that moment.

## Gotchas

- **Icon:** `src/RemoteLogViewer/app.ico` (multi-size PNG-in-ICO) is the exe icon (`ApplicationIcon` in the csproj) and `MainWindow`'s `Icon` (embedded as `Resource`). It was generated with a one-off `System.Drawing` script (log rows with level dots + green tail cursor); to change it, replace the file. Dialog windows have no `Icon` set.
- **`LogLine` must stay a class, not a record.** Value equality would make identical log lines collide in `ListBox` selection. `IsMatch` raises `INotifyPropertyChanged` for search highlighting.
- **Use `Checked`/`Unchecked` for toggles and checkboxes, not `Click`.** `Click` doesn't fire for UI Automation or programmatic toggles. `Checked` fires during `InitializeComponent` when `IsChecked="True"` is set in XAML, so handlers must guard with `IsLoaded`.
- **`ActiveSessionTests` does real SMB I/O through `\\localhost\C$`, falling back to `\\127.0.0.1\C$`** (on some machines only the IP works) as the current user. It returns early and passes silently if neither is reachable. Claude Code's sandbox blocks SMB: run these tests outside the sandbox, or they silently skip. The exception is `Local_file_tails_*`, which tails a local temp file with no SMB and always runs.
- **Menu items with dynamic names use a `TextBlock` header** so `_` in names (e.g. `DOB_SDL`) isn't eaten as an access key; they set `AutomationProperties.Name` explicitly, otherwise UI Automation/screen readers see no name.
- **Drag start for tabs is detected on the `TabControl`'s `PreviewMouseMove`**, not on the `TabItem`: a fast drag leaves the small header before the first move event.
- **`Environment.GetFolderPath(ApplicationData)` ignores the `APPDATA` env var.** Manual UI testing therefore reads and writes the real `sessions.json`. Clean it up afterwards.

## Workflow for code changes (mandatory)

1. **Ask first.** Before any request that needs code changes, ask the user whether to use spec-kit or not. Don't assume either way.
2. **If spec-kit is used**, the branch depends on where you are:
   - **Not on a `feature/*` branch** (e.g. `develop`): before implementing, create `feature/<name>`, where `<name>` is the feature name chosen by spec-kit (e.g. `feature/001-remote-log-viewer`).
   - **Already on a `feature/*` branch**: stay on it and don't create a new branch. The spec-kit feature name must be rooted in the current feature: `<current-feature>-<short-name>`. For example, on `feature/001-remote-log-viewer` it becomes `specs/001-remote-log-viewer-<short-name>/`, passed as `SPECIFY_FEATURE_DIRECTORY`.
3. **After every change**, update `README.md` and this `CLAUDE.md` if the change affects what they describe: features, commands, architecture, gotchas.
4. **After every user-visible change** (features, buttons, commands, keyboard shortcuts, messages, behaviour), update **both** user guides `src/RemoteLogViewer/Guida.it.html` and `Guida.en.html` too (self-contained HTML: no external resources; keep the shortcuts table complete; same section ids, a test checks it). Same for `README.md` (English) and `README.it.md` (Italian), and add the English text to `L.cs` for every new UI string.

## Spec-kit workflow

- Features are specified with the spec-kit skills (`/speckit-specify` → `plan` → `tasks` → `implement`) under `specs/`.
- `.specify/feature.json` (the active feature pointer) is gitignored.
- The Python scripts in `.specify/scripts/python/` need `PYTHONIOENCODING=utf-8` on this machine. Without it, output with non-ASCII characters crashes on cp1252.
- `.specify/memory/constitution.md` is still the unfilled template.
