# Tasks: Line bookmarks

**Input**: Design documents from `/specs/007-line-bookmarks/`

**Tests**: included for the pure navigation helper (project rule: pure logic is tested). UI is checked with `quickstart.md`.

## Format: `- [ ] [ID] [P?] [Story] Description with file path`

## Phase 1: Setup

None (existing project, no new dependency).

## Phase 2: Foundational (blocks all stories)

- [X] T001 [P] Tests in `tests/RemoteLogViewer.Tests/BookmarksTests.cs`: `Bookmarks.Next(lines, selected, dir)` returns the next bookmarked index after `selected` (e.g. bookmarks at 10, 50, 90, selected 20, dir +1 → 50); wraps (selected 90, dir +1 → 10; selected 10, dir -1 → 90); `selected = -1` with dir +1 → first bookmark, with dir -1 → last; selected on a bookmark moves to the *other* bookmark, and with a single bookmark that is selected returns that same index; no bookmarks → `-1`; empty list → `-1`; two identical-text lines, only one bookmarked → only that one is returned (identity)
- [X] T002 `src/RemoteLogViewer/LogLevel.cs`: add `public bool IsBookmarked` to `LogLine` (default false, raises `PropertyChanged` with `nameof(IsBookmarked)` only when the value changes, same pattern as `IsMatch`); `LogLine` stays a class
- [X] T003 Create `src/RemoteLogViewer/Bookmarks.cs`: `public static class Bookmarks` with `public static int Next(IReadOnlyList<LogLine> lines, int selected, int dir)` (linear scan with wrap, `dir` is +1/-1, returns `-1` if no line has `IsBookmarked`; must make T001 pass)

**Checkpoint**: `dotnet test --filter "FullyQualifiedName~BookmarksTests"` green.

## Phase 3: User Story 1 - Mark and clear a line (P1) 🎯 MVP

**Goal**: F2 toggles a visible bookmark on the selected line.

**Independent test**: quickstart steps 2–3.

- [X] T004 [US1] `src/RemoteLogViewer/LogView.xaml.cs`: `public static readonly RoutedCommand ToggleBookmark` (add to the existing `FindNext, FindPrev, ...` declaration) and a `CommandBinding` in the ctor: acts on `Lines.SelectedItem as LogLine`; ignore if null or `IsMarker`; flips `IsBookmarked`, then `UpdateCount()`
- [X] T005 [US1] `src/RemoteLogViewer/LogView.xaml`: `KeyBinding Key="F2"` → `ToggleBookmark` in `UserControl.InputBindings`; row context-menu `MenuItem` *Segnalibro (F2)* (with `AutomationProperties.Name`, `Command` = `ToggleBookmark`; it must stay available when the menu opens on a line, and `ContextMenuOpening` already cancels it on no line); item container `DataTrigger` on `IsBookmarked` = True → `Background` `#BBDEFB`, declared **before** the `IsMatch` trigger so a search hit (yellow) still wins; `ItemTemplate` becomes a `DockPanel` with a `TextBlock` `●` (docked left, `Foreground` SteelBlue, `Margin` 0,0,4,0, `Visibility` `Collapsed` unless `IsBookmarked`, via `DataTrigger`) before the existing text `TextBlock` (keep the wrap trigger on the text); use `{local:T '...'}` for the new header
- [X] T006 [US1] `src/RemoteLogViewer/LogView.xaml.cs` `UpdateCount`: append `L.F(" · {0} segnalibri", n)` to `CountLabel.Text` when `n = _all.Count(l => l.IsBookmarked) > 0` (FR-008)

## Phase 4: User Story 2 - Jump between bookmarks (P1)

**Goal**: F3 / Shift+F3 move between visible bookmarks when the search box is empty; search keeps priority when it has text.

**Independent test**: quickstart steps 4–5.

- [X] T007 [US2] `src/RemoteLogViewer/LogView.xaml.cs` `Find(int dir)`: where it now returns when `_search.Length == 0`, call a new `GoToBookmark(dir)` instead (if `_visible.Count == 0` return); `GoToBookmark` uses `Bookmarks.Next(_visible, Lines.SelectedIndex, dir)`; `-1` → `Info(L.T("Nessun segnalibro."))`; otherwise `FollowButton.IsChecked = false`, `Lines.SelectedIndex = i`, `Lines.ScrollIntoView(_visible[i])`. Behaviour with search text stays unchanged (FR-003)
- [X] T008 [US2] `src/RemoteLogViewer/LogView.xaml`: update the tooltips of the ▲/▼ buttons and the `SearchBox` (`Ctrl+F, F3 / Shift+F3`) so they say F3 jumps between bookmarks when the search box is empty

## Phase 5: User Story 3 - Bookmarks survive buffer changes (P2)

**Goal**: bookmarks stay on their line through filters, prepend and trim; dropped only with the line.

**Independent test**: quickstart steps 6–7.

- [X] T009 [US3] Verify (no code change expected, flag is on the `LogLine`): in `src/RemoteLogViewer/LogView.xaml.cs` `Rebuild`, `Drain` prepend, trim and `Clear_Click` nothing copies or recreates `LogLine`s; run quickstart steps 6–7 by hand with the app; fix only if a bookmark is lost or the view jumps
- [X] T010 [P] [US3] `tests/RemoteLogViewer.Tests/ContextFilterTests.cs`: a `LogLine` with `IsBookmarked = true` fed through `ContextFilter` (filter hiding it, then a new `ContextFilter` replaying with the filter off) is the same object and still bookmarked; two identical-text lines, one bookmarked, stay distinct

## Phase 6: Polish & cross-cutting

- [X] T011 `src/RemoteLogViewer/L.cs`: English entries for every new Italian string (*Segnalibro (F2)*, "Nessun segnalibro.", " · {0} segnalibri", new tooltip texts)
- [X] T012 [P] `src/RemoteLogViewer/Guida.it.html` and `Guida.en.html`: document bookmarks (same section ids in both; add a section id if needed and keep `GuideTests` green) and add rows F2 and F3 / Maiusc+F3 (bookmarks when search is empty) to the shortcuts table
- [X] T013 [P] `README.md` and `README.it.md`: feature bullet and shortcuts
- [X] T014 [P] `CHANGELOG.md`: `## [Unreleased]` → `Added`: line bookmarks (F2, F3 / Shift+F3)
- [X] T015 [P] `CLAUDE.md`: in the `LogView` paragraph add: `LogLine.IsBookmarked` (flag on the object, so it survives Rebuild/prepend/trim), `ToggleBookmark` (F2), F3/Shift+F3 → `Bookmarks.Next` over `_visible` when the search box is empty
- [X] T016 Run `dotnet build`, `dotnet test` (kill `RemoteLogViewer.exe` first if the build is locked) and the full `quickstart.md`

## Dependencies

- Phase 2 first (T002 → T003; T001 parallel with T002). US1 needs T002. US2 needs T003 and T004's `UpdateCount` is independent. US3 after US1+US2. Polish last.
- Within US1: T004 → T005 → T006 (same files); T007 → T008.

## Parallel example

T001 and T002 together; T012–T015 together at the end.

## Implementation strategy

MVP = Phases 2–3 (mark and clear). Then US2 (navigation), US3 (verification), polish.
