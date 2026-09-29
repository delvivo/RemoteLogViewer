---
description: "Task list for Remote Log Viewer"
---

# Tasks: Remote Log Viewer

**Input**: Design documents from `specs/001-remote-log-viewer/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: xUnit per logica non-UI (richiesto da plan.md: tailer, livelli, store). UI validata con quickstart.md.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup

- [ ] T001 Create `RemoteLogViewer.sln`, WPF project `src/RemoteLogViewer/RemoteLogViewer.csproj` (`net10.0-windows`, `UseWPF`, `Nullable enable`) and xUnit project `tests/RemoteLogViewer.Tests/RemoteLogViewer.Tests.csproj` (`net10.0-windows`) referencing the app project
- [ ] T002 Add package `System.Security.Cryptography.ProtectedData` to `src/RemoteLogViewer/RemoteLogViewer.csproj`; add `.gitignore` for `bin/`, `obj/`, `.vs/`

---

## Phase 2: Foundational

- [ ] T003 [P] Create `SessionConfig` in `src/RemoteLogViewer/SessionConfig.cs`: Id (Guid), Name (obbligatorio), SharePath (`\\server\share`), UserName (vuoto = utente corrente), ProtectedPassword (base64 DPAPI, mai in chiaro), FilePath (relativo → combinato con SharePath; UNC completo), TailLines (default 1000, 0..100000), Encoding (`auto` default, `utf-8`, `utf-16`, `windows-1252`); computed `FullPath`; `Validate()` returning error list
- [ ] T004 [P] Create `LogLine` record (Text, Level `None|Debug|Info|Warn|Error`, IsMarker) and `LogLevels.Detect(string, LogLevel previous)` per research §6 in `src/RemoteLogViewer/LogLevel.cs`
- [ ] T005 [P] Implement `SmbConnection` in `src/RemoteLogViewer/SmbConnection.cs`: P/Invoke `WNetAddConnection2W`/`WNetCancelConnection2W` (no local name), ref-counted per share, cancel only if opened by app, error code → Italian message map (5, 53, 67, 85=OK, 86, 1219, 1326) per research §1; `ReleaseAll()` for app exit

**Checkpoint**: modelli e connessione pronti.

---

## Phase 3: User Story 1 - Seguire un log remoto in tempo reale (P1) 🎯 MVP

**Goal**: avviare una sessione (inserita a mano) e vedere ultime N righe + nuove righe entro 2 s.

**Independent Test**: quickstart scenari 1, 2, 6.

- [ ] T006 [P] [US1] Tests in `tests/RemoteLogViewer.Tests/LogTailerTests.cs`: tail last N of 2000 lines, N > file lines, appended lines, partial line held until `\n`, truncation → marker + restart from 0, UTF-16 BOM decoding
- [ ] T007 [P] [US1] Tests in `tests/RemoteLogViewer.Tests/LogLevelTests.cs`: ERROR/WARN/INFO/DEBUG detection, word boundary (`INFORMATION` not needed; `ERRORS` no), continuation lines inherit previous level
- [ ] T008 [US1] Implement `LogTailer` in `src/RemoteLogViewer/LogTailer.cs`: `ReadTail(n)` reading backwards 64 KB blocks; `Poll()` returning new lines; FileShare.ReadWrite|Delete, short-lived handle per poll; detect shrink or creation-time change → rotation marker; BOM detect + stateful decoder; `windows-1252` via `CodePagesEncodingProvider`
- [ ] T009 [US1] Implement `ActiveSession` in `src/RemoteLogViewer/ActiveSession.cs`: states `Connecting|Running|Reconnecting|Error|Stopped`; background loop (connect share → ReadTail → Poll every 500 ms); IOException → Reconnecting, retry every 5 s resuming offset; auth/path errors on first connect → Error (no retry); `ConcurrentQueue<LogLine>` pending lines; `Stop()` releases share
- [ ] T010 [US1] Create `LogView` UserControl in `src/RemoteLogViewer/LogView.xaml(.cs)`: status text, Stop/Avvia, Pulisci; virtualized ListBox (Recycling) monospace; DispatcherTimer 200 ms draining queue in batch; buffer cap 100.000 trimmed in 10.000 blocks; auto-scroll to end
- [ ] T011 [US1] Create `MainWindow` in `src/RemoteLogViewer/MainWindow.xaml(.cs)` with TabControl hosting LogView per session (tab header = name + status color + close); `App.xaml(.cs)` calls `SmbConnection.ReleaseAll()` on exit
- [ ] T012 [US1] Create `SessionEditWindow` in `src/RemoteLogViewer/SessionEditWindow.xaml(.cs)` per contracts/ui.md form (Nome, Share, Utente, Password, File, Righe, Encoding) with validation and **Test connessione**; "Avvia" opens a tab

**Checkpoint**: MVP — sostituisce lo script PowerShell.

---

## Phase 4: User Story 2 - Salvare e riusare sessioni (P2)

**Independent Test**: quickstart scenario 3.

- [ ] T013 [P] [US2] Tests in `tests/RemoteLogViewer.Tests/SessionStoreTests.cs`: save/load round-trip in temp dir, password not present in plaintext in file, missing file → empty, corrupt file → `.bak` + empty, undecryptable password → null
- [ ] T014 [US2] Implement `SessionStore` in `src/RemoteLogViewer/SessionStore.cs` per contracts/sessions-file.md: `%APPDATA%\RemoteLogViewer\sessions.json`, camelCase System.Text.Json, atomic write (temp + `File.Replace`/`Move`), `Protect/Unprotect` DPAPI CurrentUser with entropy
- [ ] T015 [US2] Add saved sessions list panel to `src/RemoteLogViewer/MainWindow.xaml(.cs)`: Nuova, Modifica, Duplica, Elimina (conferma), Avvia (doppio click); SessionEditWindow saves via SessionStore; empty password on edit = unchanged

---

## Phase 5: User Story 3 - Più sessioni in parallelo (P2)

**Independent Test**: quickstart scenario 4.

- [ ] T016 [US3] Add **Affianca** toggle to `src/RemoteLogViewer/MainWindow.xaml(.cs)`: show all open LogViews in a UniformGrid (reparent controls, tails keep running); toggle back restores tabs
- [ ] T017 [US3] Verify per-session isolation in `src/RemoteLogViewer/ActiveSession.cs`: errors confined to own session; same share shared via SmbConnection ref count

---

## Phase 6: User Story 4 - Visualizzazione intelligente (P3)

**Independent Test**: quickstart scenario 5.

- [ ] T018 [US4] Level colors in `src/RemoteLogViewer/LogView.xaml`: DataTriggers Error red, Warn orange, Debug gray, marker italic
- [ ] T019 [US4] Follow toggle in `src/RemoteLogViewer/LogView.xaml(.cs)`: scroll up → Follow off; Follow on or scroll to end → resume auto-scroll
- [ ] T020 [US4] Filter (text/regex checkbox, invalid regex → red border, no crash) in `src/RemoteLogViewer/LogView.xaml(.cs)`: rebuild visible list from full buffer on change; incoming lines filtered
- [ ] T021 [US4] Search in `src/RemoteLogViewer/LogView.xaml(.cs)`: highlight matching rows, ▲▼ and F3/Shift+F3 select+scroll to prev/next (turns Follow off)
- [ ] T022 [US4] Multi-select + Ctrl+C copy selected lines in `src/RemoteLogViewer/LogView.xaml(.cs)`

---

## Phase 7: Polish

- [ ] T023 Add `README.md` with build/run instructions and usage
- [ ] T024 Run `dotnet build` + `dotnet test` and quickstart.md scenarios 1-6 against a local share

---

## Dependencies

- Setup → Foundational → US1 → (US2, US3, US4 independent of each other, all build on US1's LogView/MainWindow)
- Within US1: T006/T007 before T008; T008 → T009 → T010 → T011 → T012

## Parallel Opportunities

- T003, T004, T005 (different files)
- T006, T007 (tests); T013 alongside US3/US4 work
- US2, US3, US4 phases touch different areas; US3 and US2 both edit MainWindow → sequential

## Implementation Strategy

MVP = Phase 1-3 (tail di una sessione, equivalente allo script). Poi US2 (persistenza), US3 (affianca), US4 (vista intelligente), validando ogni fase con quickstart.
