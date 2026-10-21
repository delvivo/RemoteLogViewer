# Changelog

All notable changes to Remote Log Viewer. Format based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), versions follow [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- **Load whole file**: new *⏫ All* button next to *▲ Previous* loads every remaining line back to the start of the file in one go. It stays within the 100,000-line buffer: for longer files the most recent 100,000 lines stay loaded.

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

[1.2.0]: https://github.com/delvivo/RemoteLogViewer/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/delvivo/RemoteLogViewer/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/delvivo/RemoteLogViewer/releases/tag/v1.0.0
