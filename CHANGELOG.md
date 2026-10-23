# Changelog

All notable changes to Remote Log Viewer. Format based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), versions follow [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- **Line bookmarks**: `F2` sets or clears a bookmark on the selected line (● marker and light blue background, also in the line context menu); with an empty search box `F3` / `Shift+F3` jump to the next / previous visible bookmark, wrapping around (with search text they keep jumping between matches). The flag lives on the line, so bookmarks survive filters, loading older lines and rebuilds, and go away with the line on trim or *Clear*. The count appears next to the line count. Not saved, not in the Merged view.
- **Merged timeline** (toolbar *Merge…*, `Ctrl+Shift+M`): a *Merged* tab that interleaves the lines of all open sessions in timestamp order, with a coloured session column. Lines without a timestamp stay attached to the previous line of their session; it starts from the lines already read and stays live (late lines and loaded older lines land in their chronological place). Per-session show/hide, level and text/regex filters with context, double-click or *Go to source tab* reveals the line in its tab, `Ctrl+S` saves `[session] line`. Closed sessions keep their rows. The tab raises no alerts and is not restored at startup. With more than two sessions open, a window lets you choose which ones to merge.
- **Rename tab**: double-click a tab title or right-click → *Rename tab…* to give an open tab (session or Merged) a name of your own. It is not saved, keeps the date suffix, and is used in Side by side, search, AI analysis and the Merged session column; typing the original name restores it.

## [1.3.0] - 2026-10-22

### Added

- **Error histogram**: a strip above each tab with one bar per minute (ERROR entries and custom alert lines, history included), over the whole log in a horizontally scrollable strip (last 50,000 minutes at most) that can be hidden with the *Histogram* button. Hover shows the count; clicking a bar goes to the first visible line of that minute, like *Go to time*.
- **Explain this line**: context-menu entry on a log line that opens the AI analysis window pre-filled with that tab only, a new *Explain this line* prompt and the line with 20 visible lines of context on each side.
- **Load whole file**: new *⏫ All* button next to *▲ Previous* loads every remaining line back to the start of the file in one go. It stays within the 100,000-line buffer: for longer files the most recent 100,000 lines stay loaded.
- **Custom levels**: per-session regexes for ERROR, WARN, INFO and DEBUG, for logs with non-standard level markers (`[E]`, `|ERR|`, `level=50`). The standard detection still applies when none matches.
- **Custom alerts**: a per-session regex; new matching lines raise the same tab badge and taskbar flash as errors.
- **Go to time** (`Ctrl+G`): selects the first visible line at or after a given time.
- **.gz archives**: rotated logs compressed by the server (`*.gz`, local or remote) open read-only, decompressed once with no tail (last 100,000 lines, no alerts). With *Today's file has no date*, a `….log.gz` path opens the uncompressed `….log` for today. `{n}` in a `.gz` file name matches numbered parts of the same day (`hr-portal.log.2026-10-03.0.gz`, `.1.gz`, …), read in number order.

### Changed

- Documentation screenshots and live-tail GIF retaken to show the new toolbar buttons and the error histogram strip.

## [1.2.0] - 2026-10-21

### Added

- **AI analysis** (toolbar *AI analysis…*, `Ctrl+Shift+I`): sends the visible lines of the chosen tabs, with a prompt, to an external AI CLI and shows the answer as it arrives.
  - Presets for Claude Code, Codex, OpenCode and Antigravity CLI, plus a custom command. Each command can be edited and reset.
  - Prompt presets *Error report* and *Summary*, or a free prompt (the last one is remembered).
  - Up to 1 MB of log per analysis, split fairly across tabs, keeping the most recent lines.
  - Confirmation the first time each tool is used. The window states that the logs leave the app.
  - The answer can be cancelled, copied, opened formatted in the browser, or exported as `.md` or `.html`.
  - While the tool runs, the status bar shows the elapsed time and its last error line, so a tool retrying a rate-limited provider no longer looks stuck.
  - A CLI installed while the app is running is found without restarting it.
- **Windows installer** (`RemoteLogViewer-<version>-setup.exe`): a per-user install that upgrades in place. It is attached to each release next to the portable zip.

## [1.1.0] - 2026-10-02

### Added

- **Workspace restore**: open tabs, selected tab and side-by-side layout come back at the next start.
- **Error alerts**: unread error badges on tabs, and the taskbar flashes when errors arrive while the window is inactive.
- **Context lines** around filter matches (`grep -C N` style).
- **Load previous lines** above the initial tail.
- **User guide** (`F1`).
- **Date dialog** shows which days have a log file.
- **Undated current file**: today's log without a date in its name (`app.log`), with dated archives (`app.2026-10-20.log`).
- **Italian/English UI**, switchable live, plus an English README and guide.
- **Advanced search** across all open tabs (`Ctrl+Shift+F`): text/regex, whole word, levels, time range, export.
- **Local files**: open them with *Open file…* (`Ctrl+O`) or by drag & drop.
- GitHub Actions workflow that publishes the portable zip on every tag.

### Fixed

- Dragging the log's scrollbar no longer starts a tab drag.

## [1.0.0] - 2026-09-30

### Added

- First release: tail log files on SMB shares (no drive mapping) in tabs or side by side.
  - Follow mode, text/regex filter and filter by level.
  - Automatic reconnection, and handling of log rotation.
- Saved sessions organised in collections, with drag & drop. Collections can be duplicated, exported and imported.
- `{date:…}` placeholders in paths.
- Shared credentials, with passwords protected by DPAPI.
- Word wrap toggle, and the application icon.

[1.3.0]: https://github.com/delvivo/RemoteLogViewer/compare/v1.2.0...v1.3.0
[1.2.0]: https://github.com/delvivo/RemoteLogViewer/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/delvivo/RemoteLogViewer/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/delvivo/RemoteLogViewer/releases/tag/v1.0.0
