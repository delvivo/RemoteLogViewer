# Implementation Plan: Custom levels, custom alerts and go to time

**Branch**: `feature/004-levels-alerts-goto` | **Date**: 2026-10-22 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/004-levels-alerts-goto/spec.md`

## Summary

Per-session regex overrides for the four levels plus an alert regex, evaluated on the session's background thread when each `LogLine` is built (so the UI thread pays nothing), and a *Go to time* command in `LogView` backed by a pure timestamp lookup over the visible lines.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0-windows`), WPF

**Primary Dependencies**: none beyond the WindowsDesktop framework (no NuGet in the app)

**Storage**: `%APPDATA%\RemoteLogViewer\sessions.json` (existing; five new optional string fields per session, v1-compatible)

**Testing**: xUnit (`tests/RemoteLogViewer.Tests`)

**Target Platform**: Windows desktop

**Project Type**: desktop-app

**Performance Goals**: pattern evaluation per line bounded (50 ms regex timeout, like the filter); go to time over 100,000 lines < 1 s (single linear scan)

**Constraints**: built-in detection unchanged when no pattern is set; old `sessions.json` loads unchanged

**Scale/Scope**: buffer up to 100,000 lines per tab

## Constitution Check

`.specify/memory/constitution.md` is still the unfilled template: no gates. Project rules from `CLAUDE.md` applied instead: pure logic tested, every new string in `L.En`, both guides + both READMEs updated, `CLAUDE.md` updated.

## Project Structure

### Documentation (this feature)

```text
specs/004-levels-alerts-goto/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
└── tasks.md
```

### Source Code (repository root)

```text
src/RemoteLogViewer/
├── LogLevel.cs            # + LineRules (custom levels + alert), LogLine.IsAlert, LogLevels.Make(rules)
├── LogTimestamp.cs        # + ParseTarget (user input) + FindAtOrAfter (pure lookup)
├── SessionConfig.cs       # + ErrorPattern/WarnPattern/InfoPattern/DebugPattern/AlertPattern, validation
├── ActiveSession.cs       # builds LineRules once from Config, passes it to Make
├── LogView.xaml(.cs)      # alerts counted with errors; Go to time button + Ctrl+G
├── SessionEditWindow.xaml(.cs) # "Livelli e avvisi" expander with five fields
├── TextDialog.xaml.cs     # configurable empty-input message
├── L.cs                   # English strings
├── Guida.it.html / Guida.en.html
tests/RemoteLogViewer.Tests/
├── LogLevelTests.cs       # LineRules
├── LogTimestampTests.cs   # ParseTarget / FindAtOrAfter
└── SessionConfigTests.cs  # pattern validation
README.md, README.it.md, CLAUDE.md, CHANGELOG.md
```

**Structure Decision**: single project, extend existing files; no new source file.

## Design

- **`LineRules`** (pure): built from a `SessionConfig`; compiles the non-empty patterns (`IgnoreCase`, 50 ms timeout). An invalid pattern (possible only via a hand-edited or imported file, since the editor validates) is ignored rather than crashing the session. `Detect(line)` = first matching custom pattern in severity order, else `LogLevels.Detect(line, None)`. `IsAlert(line)`. `LineRules.Default` = no patterns. `LineRules.Check(pattern)` returns null or the parser message.
- **`LogLine.IsAlert`**: set by `LogLevels.Make` only for non-history lines. `LogView.Drain` counts a line once if `!IsHistory && (Error entry || IsAlert)` → existing `UnreadErrors` / `ErrorsArrived` path, so badge and taskbar flash need no change.
- **Go to time**: `LogTimestamp.TryParseTarget(text, defaultDate, out value)` accepts `H:mm[:ss]`, `yyyy-MM-dd H:mm[:ss]`, `dd/MM/yyyy H:mm[:ss]` (time only → `defaultDate`). `LogTimestamp.FindAtOrAfter(lines, target, defaultDate)` returns `(index, status)` with status `Found`, `BeforeFirst` (found at the first timestamped line, target earlier), `NotFound`, `NoTimestamps`; markers/separators are skipped, lines without a timestamp inherit the previous one. `LogView` runs it over `_visible`, selects/scrolls, turns Follow off, and shows the messages (for `BeforeFirst` only when `Session.CanLoadOlder`).
- **UI**: `LogView` button *Vai all'ora…* + `Ctrl+G` (`RoutedCommand GoToTime`); `TextDialog.Ask` with a validator (gets an `emptyError` argument). `SessionEditWindow` adds a collapsed `Expander` (auto-expanded when any pattern is set).

## Complexity Tracking

None.
