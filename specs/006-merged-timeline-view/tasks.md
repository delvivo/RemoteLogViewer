# Tasks: Merged timeline view

**Input**: Design documents from `/specs/006-merged-timeline-view/`

**Tests**: included for the pure logic (project rule: pure logic is tested). UI is checked with `quickstart.md`.

## Format: `- [ ] [ID] [P?] [Story] Description with file path`

## Phase 1: Setup

None (existing project, no new dependency).

## Phase 2: Foundational (blocks all stories)

- [X] T001 [P] Tests in `tests/RemoteLogViewer.Tests/TimelineMergeTests.cs`: two sessions interleave by timestamp (A 10:00:01, B 10:00:02, A 10:00:03 → A, B, A); a line without timestamp (stack trace) inherits and stays right after its head line even when the other session has a line in between in time; marker lines inherit; equal timestamps from two sessions order by session index then `Seq`, same result when fed in the opposite order; `Add` returns `true` for in-order batches and `false` when a late line is inserted in the middle (and the row lands at its chronological place); prepended block lands before existing rows of that session in file order; a session with no parsable timestamp sorts first (`MinValue`) in arrival order and keeps `HasTimestamps == false`; time-only timestamps use the session `DefaultDate`; `Trim()` removes the oldest 20,000 rows when `Rows.Count > 200,000` and returns `true`, otherwise `false`; copies: mutating `IsContext` on a `MergedLine` does not touch `Original`; after a prepend, the leading no-timestamp rows of that session's old head (previously `MinValue`) take the effective time of the prepended block's last row and are re-sorted with it (SC-002: no misplaced rows); load: two sessions of 100,000 interleaved lines added with one `Add` each complete in under 3 seconds (generous limit, guards against per-line O(n) inserts)
- [X] T002 Create `src/RemoteLogViewer/TimelineMerge.cs`, pure, no WPF: `MergedSession` (`Index`, `Name`, `DefaultDate`, `Source` (`LogView?`), `Closed`, `Visible`, `HasTimestamps`, internal `Last` starting `DateTime.MinValue`, `NextSeq`, `MinSeq`); `MergedLine : LogLine` (copy of `Text`, `Level`, `IsMarker`, `IsEntry`, `IsHistory`, `IsAlert`; plus `Original`, `Session`, `Time`, `Seq` (long, negative for prepended blocks)); `TimelineMerge` with `AddSession(name, defaultDate, source = null)`, `bool Add(session, IReadOnlyList<LogLine>, bool prepend = false)` (effective time via `LogTimestamp.TryParse`, inherit `Last` for no-timestamp and marker lines; sort key `(Time, Session.Index, Seq)` ascending; **batch merge**: sort the new batch, then if its first key is ≥ the last row append, else merge it with `Rows` linearly in one pass (O(n+m), never one `List.Insert` per line: the initial snapshot of a second 100,000-line session would otherwise cost ~10^10 element moves); returns `true` only when no existing index shifted; on `prepend` also re-time the session's leading no-timestamp rows as described in T001), `IReadOnlyList<MergedLine> Rows`, `bool Trim()` (`MaxRows` = 200,000, `TrimBlock` = 20,000, oldest first). Add a `// ponytail:` note: the merge is O(n) per out-of-order tick, incremental insert if profiling shows it
- [X] T003 `src/RemoteLogViewer/LogView.xaml.cs`: add events `Action<IReadOnlyList<LogLine>>? LinesAppended` (raised in `Drain` with the lines dequeued from `Pending` in that tick, after they are added to `_all`, only if non-empty), `Action<IReadOnlyList<LogLine>>? LinesPrepended` (one call per `Older` block, in file order), `Action? Closed` (raised at the end of `Close()`); no behaviour change otherwise (*Pulisci* and the 100k trim raise nothing)

**Checkpoint**: `dotnet test --filter "FullyQualifiedName~TimelineMergeTests"` green, existing tests green.

## Phase 3: User Story 1 - Chronological merge of selected sessions (P1) 🎯 MVP

**Independent test**: two local files with interleaved timestamps (quickstart steps 1–3): the merged tab lists A, B, A (+ the stack-trace line) with a coloured session column; unchecking a session hides its rows.

- [X] T004 [US1] Create `src/RemoteLogViewer/MergedView.xaml`: `UserControl` with a top `WrapPanel` (Follow `ToggleButton`, *A capo* `ToggleButton`, *Salva…* button bound to `ApplicationCommands.Save`, filter `TextBox` + *Regex* `CheckBox` + *Contesto* `TextBox`, four level `CheckBox`es with `Tag` = level name and counts in `Content`, `ItemsControl`/`WrapPanel` of per-session `CheckBox`es with a coloured swatch and name, `CountLabel`) and a virtualized `ListBox Lines` (`SelectionMode="Extended"`, `Cascadia Mono, Consolas` 12, recycling) like `LogView.xaml`; item container style with the same level/`IsContext`/`IsMarker` triggers as `LogView.xaml`; item template = `Grid` with a fixed-width (120) coloured session cell (background = session colour at low opacity, name trimmed with ellipsis, tooltip with the full name) and the line `TextBlock` (wrap when *A capo* is checked). Use `{local:T '...'}` for every Italian string; toggles use `Checked`/`Unchecked` with `IsLoaded` guards
- [X] T005 [US1] Create `src/RemoteLogViewer/MergedView.xaml.cs`: ctor `MergedView(IReadOnlyList<LogView> sources)`; one `MergedSession` per source (name = `Session.DisplayName`, `DefaultDate = Session.Date ?? DateTime.Today`, colour from a fixed 8-colour palette by index, readable on a light background), one `Add(session, source.Snapshot())` per session (a single batch each, see T002), then subscribe to its `LinesAppended`/`LinesPrepended`/`Closed`; `_visible` `ObservableCollection<MergedLine>`; `Rebuild(bool keepTopLine = false)` = new `ContextFilter(ContextLines, l => LevelOk(l) && ((MergedLine)l).Session.Visible, text)` replayed over `Rows` (use `Rows` as the `IReadOnlyList<LogLine>`), keeping the top item as in `LogView.Rebuild`; no timer: react directly to the `LogView` events (UI thread): if `Add` returned `true` feed only the new rows to `_ctx.Add` and append to `_visible`, else `Rebuild(keepTopLine: true)`; after `Add` call `Trim()` and `Rebuild()` if it removed rows; per-level entry counts recomputed on rebuild and updated incrementally; filter/regex/context/level handlers as in `LogView` (invalid regex → red border); session checkboxes toggle `Session.Visible` and `Rebuild()`; Follow with `ScrollToEnd` and `Lines_ScrollChanged` as in `LogView`; `Ctrl+C` copies the selected rows' texts; `Close()` unsubscribes from every source; `Title` property ("Unione (n)"). Do NOT subscribe to `ErrorsArrived` or touch `UnreadErrors`: the merged view raises no alerts, flashes nothing and has no badge (FR-012)
- [X] T006 [US1] `src/RemoteLogViewer/MainWindow.xaml(.cs)`: `public static readonly RoutedCommand Merge`; `CommandBinding` + `KeyBinding` `Ctrl+Shift+M` + toolbar `Button` *Unisci…* with a tooltip (next to *Cerca…*/*Analisi IA…*); `Merge_Executed`: sources = `Tabs.Items.Cast<TabItem>().Select(t => t.Tag).OfType<LogView>()`; fewer than 2 → `MessageBox` info "Servono almeno due sessioni aperte."; if a merged tab already exists select it and show a short info that new sessions are not added; else build `MergedView`, a `TabItem` (header = title `TextBlock` + close `Button`, `Tag` = view, middle-click close, same drag/drop reorder handlers as `Open`) and select it. Extract the tab drag/drop wiring from `Open` into a shared helper so both tab kinds use it
- [X] T007 [US1] `src/RemoteLogViewer/MainWindow.xaml.cs`: make the code that assumed `(LogView)t.Tag` tolerate a `MergedView`: `OpenFiles` and `LocalConfig` (`OfType<LogView>()`), `Search_Executed` and `ShowAi` source lists (`OfType<LogView>()`), `CloseTab` (call `Close()` on whichever type), `Relayout` (take the `UserControl` from `Tag`; title in side-by-side = `DisplayName` for `LogView`, `MergedView.Title` otherwise), `Window_Closing` (workspace tabs from `LogView`s only; `Selected` = index of the selected tab among the session tabs only; when the merged tab is selected, store the index of the session tab that was selected before it (track it on `SelectionChanged`), or 0 if none; still `Close()` every view), `Window_Loaded` (the existing bounds guard on `ws.Selected` stays)
- [X] T008 [US1] `src/RemoteLogViewer/L.cs`: English entries for every new Italian string of T004–T007 (*Unisci…*, tooltips, "Unione", "Servono almeno due sessioni aperte.", etc.)

**Checkpoint**: quickstart steps 1–3 and 9 pass; opening/closing the merged tab does not break search, AI window, workspace save/restore, *Affianca*.

## Phase 4: User Story 2 - Live updates (P2)

**Independent test**: with the merged tab open append lines to both files (quickstart steps 4, 6).

- [X] T009 [US2] `src/RemoteLogViewer/MergedView.xaml.cs`: on `Closed` of a source set `Session.Closed = true`, unsubscribe from that source, refresh the session checkbox label/tooltip ("(chiusa)"), keep its rows; on `LinesPrepended` call `Add(..., prepend: true)` and `Rebuild(keepTopLine: true)`; confirm *Pulisci* in a source leaves merged rows (no handler needed) and new lines still arrive; late line insert covered by the `Add == false` path from T005
- [X] T010 [US2] `src/RemoteLogViewer/MergedView.xaml.cs`: show a notice in the session list (tooltip or label suffix) for a session whose `HasTimestamps` is false after the initial snapshot ("nessun orario riconoscibile: righe in testa"); update it as lines arrive
- [X] T011 [US2] `src/RemoteLogViewer/L.cs`: English entries for T009–T010 strings

**Checkpoint**: quickstart steps 4 and 6 pass.

## Phase 5: User Story 3 - Filter and navigate (P3)

**Independent test**: quickstart steps 3, 5, 7.

- [X] T012 [US3] `src/RemoteLogViewer/MergedView.xaml(.cs)`: `RevealRequested` event `Action<LogView, LogLine>`; raised on double-click of a row and from a row context menu entry *Vai al tab sorgente* (menu built on `ContextMenuOpening` from the item under the mouse, cancelled on no line/marker/closed session; double-click on a row of a closed session does nothing, its session cell tooltip says "sessione chiusa"; item header is a `TextBlock` with `AutomationProperties.Name`); `Ctrl+S` binding `ApplicationCommands.Save` saving the visible rows as `[session] text` (UTF-8 without BOM, file name `Unione_{yyyyMMdd_HHmmss}.log`, same `SaveFileDialog` pattern and error handling as `LogView.SaveVisible`)
- [X] T013 [US3] `src/RemoteLogViewer/MainWindow.xaml.cs`: subscribe `MergedView.RevealRequested` to the existing `RevealLine` (extend it to pass `MergedLine.Original`); on `RevealResult.NotFound` show an info message that the line is no longer in the source buffer; with *Affianca* on do not change the selected tab (already how `RevealLine` behaves)
- [X] T014 [US3] `src/RemoteLogViewer/L.cs`: English entries for T012–T013 strings

**Checkpoint**: quickstart steps 3, 5 and 7 pass.

## Phase 6: Polish & cross-cutting

- [X] T015 [P] `src/RemoteLogViewer/Guida.it.html` and `Guida.en.html`: new section (same id in both, e.g. `merged`) describing the merged tab, session column/colours, per-session checkboxes, live behaviour, closed sessions, *Vai al tab sorgente*, save; add `Ctrl+Maiusc+M` / `Ctrl+Shift+M` to the shortcuts table; add the section to the table of contents if the guides have one (`GuideTests` checks equal ids)
- [X] T016 [P] `README.md` and `README.it.md`: feature bullet and shortcut
- [X] T017 [P] `CHANGELOG.md`: entry under `## [Unreleased]` → `Added` ("Merged timeline view: …"), English
- [X] T018 [P] `CLAUDE.md`: architecture paragraph under `MainWindow` (or a new item) for `TimelineMerge`/`MergedView`, the `LogView` events, `MergedLine` being a copy (why), the merged tab not in the workspace and the `Tag` can be a `MergedView`; update the Tab-related gotchas if needed
- [ ] T019 Run `dotnet build` (kill a running `RemoteLogViewer.exe` first) and `dotnet test` (ActiveSession SMB tests run outside the sandbox or skip), then walk through `quickstart.md` manually; remove any test residue from `%APPDATA%\RemoteLogViewer\sessions.json`
  - Done: build + 245 tests green, automated smoke (two local files → merged order). Still to do by hand: the quickstart UI walk-through (tab, colours, drag, Affianca, language switch).

## Phase 7: Follow-up (post-review)

- [X] T020 `src/RemoteLogViewer/MergeDialog.xaml(.cs)` + `MainWindow.Merge_Executed`: with > 2 session tabs a dialog lists them (all ticked, OK needs ≥ 2) and only the chosen ones are merged; with exactly 2 no dialog
- [X] T021 Rename tabs: `ActiveSession.Alias/Label`, `LogView.Rename`, `MergedView.CustomTitle`, `MainWindow.WireTabRename/RenameTab` (header double-click + context menu), `MergedView` follows source renames; tests in `tests/RemoteLogViewer.Tests/ActiveSessionAliasTests.cs`; strings in `L.cs`, guides, READMEs, CHANGELOG, CLAUDE.md

## Dependencies & execution order

- Phase 2 first: T001 and T002 (T001 can be written first, TDD); T003 is independent of them ([P]-able, but touches `LogView.xaml.cs` only).
- US1 needs T002 and T003. Within US1: T004 → T005 (same view), T006/T007 need T005, T008 last.
- US2 and US3 extend `MergedView.xaml(.cs)` and `MainWindow.xaml.cs`: do after US1 (T009–T010 before T012–T013 to avoid conflicts in the same file).
- Polish after the stories; T015–T018 are independent files and can run in parallel.

## Implementation strategy

MVP = Phase 2 + US1 (merged tab with colour column, per-session show/hide, filters, live append at the end of the list is already included through the events). US2 adds the out-of-order/prepend/closed-session polish, US3 navigation and saving. Commit after each phase.
