# Tasks: Custom levels, custom alerts and go to time

**Input**: Design documents from `/specs/004-levels-alerts-goto/`

**Tests**: included for the pure logic (project rule: pure logic is tested).

## Phase 1: Setup

None (existing project).

## Phase 2: Foundational

- [X] T001 Add `ErrorPattern`, `WarnPattern`, `InfoPattern`, `DebugPattern`, `AlertPattern` (string, default `""`) to `src/RemoteLogViewer/SessionConfig.cs`; `Validate()` adds a localized error naming the field for each non-empty invalid regex
- [X] T002 Add `LineRules` (compiled `IgnoreCase` regexes, 50 ms timeout, invalid patterns ignored, `Default`, `Detect`, `IsAlert`, `Check`) and `LogLine.IsAlert` to `src/RemoteLogViewer/LogLevel.cs`; `LogLevels.Make` takes optional `LineRules`, sets `IsAlert` only when not history
- [X] T003 `src/RemoteLogViewer/ActiveSession.cs`: build `new LineRules(config)` once, pass it to both `LogLevels.Make` calls

## Phase 3: User Story 1 - Custom level patterns (P1)

**Independent test**: session with ERROR `\[E\]` classifies `[E] boom` as Error.

- [X] T004 [P] [US1] Tests for `LineRules.Detect` (custom, fallback, severity order, inheritance via `Make`, invalid ignored) in `tests/RemoteLogViewer.Tests/LogLevelTests.cs`
- [X] T005 [P] [US1] Tests for pattern validation in `tests/RemoteLogViewer.Tests/SessionConfigTests.cs`
- [X] T006 [US1] `Expander` *Livelli e avvisi* with ERROR/WARN/INFO/DEBUG fields in `src/RemoteLogViewer/SessionEditWindow.xaml` + read/write in `SessionEditWindow.xaml.cs` (auto-expanded when any pattern set)

## Phase 4: User Story 2 - Custom alert (P2)

**Independent test**: alert `deadlock`, new matching line while tab hidden → badge.

- [X] T007 [P] [US2] Tests for `IsAlert` (match, history never alerts, empty pattern) in `tests/RemoteLogViewer.Tests/LogLevelTests.cs`
- [X] T008 [US2] Alert field in the expander (`SessionEditWindow.xaml/.cs`)
- [X] T009 [US2] `src/RemoteLogViewer/LogView.xaml.cs` `Drain`: count `!IsHistory && (Error entry || IsAlert)` once into the existing errors/unread path

## Phase 5: User Story 3 - Go to time (P3)

**Independent test**: lines 10:00/14:30/14:35, go to 14:32 → 14:35 selected.

- [X] T010 [P] [US3] `LogTimestamp.TryParseTarget` and `LogTimestamp.FindAtOrAfter` + `GoToStatus` in `src/RemoteLogViewer/LogTimestamp.cs`
- [X] T011 [P] [US3] Tests in `tests/RemoteLogViewer.Tests/LogTimestampTests.cs`
- [X] T012 [US3] `TextDialog.Ask` optional `emptyError` in `src/RemoteLogViewer/TextDialog.xaml.cs`
- [X] T013 [US3] `GoToTime` command, `Ctrl+G` binding and *Vai all'ora…* button in `src/RemoteLogViewer/LogView.xaml(.cs)`: select + scroll, Follow off, messages for NotFound/NoTimestamps/BeforeFirst (only if `Session.CanLoadOlder`)

## Phase 6: Polish

- [X] T014 English strings for every new text in `src/RemoteLogViewer/L.cs`
- [X] T015 [P] Guides `src/RemoteLogViewer/Guida.it.html` / `Guida.en.html` (sections + shortcuts table `Ctrl+G`)
- [X] T016 [P] `README.md`, `README.it.md`, `CHANGELOG.md` (Unreleased), `CLAUDE.md`
- [X] T017 `dotnet build` + `dotnet test` green

## Dependencies

T001–T003 before all stories. US1, US2, US3 independent afterwards (US1/US2 share `SessionEditWindow`, do sequentially). Polish last.

## Implementation strategy

MVP = Phase 2 + US1. Then US2, US3, Polish.
