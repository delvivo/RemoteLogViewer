# Tasks: Error histogram and "Explain this line" with AI

**Input**: Design documents from `/specs/005-error-histogram-explain-line/`

**Tests**: included for the pure logic (project rule: pure logic is tested). UI is checked with `quickstart.md`.

## Phase 1: Setup

None (existing project).

## Phase 2: Foundational

None (the two stories are independent and touch different files except `LogView.xaml(.cs)`, `MainWindow.xaml.cs` and `L.cs`; do US1 first, then US2).

## Phase 3: User Story 1 - Errors-per-minute histogram (P1)

**Independent test**: log with 2 errors at 14:31, 9 at 14:32, 1 at 14:35: 14:32 bar tallest, click selects the first 14:32 line.

- [X] T001 [P] [US1] Tests in `tests/RemoteLogViewer.Tests/ErrorHistogramTests.cs`: Error entry and alert line counted; history lines counted; non-entry stack-trace lines not counted; non-error levels not counted; lines without timestamp inherit previous minute; markers ignored; nothing counted before the first timestamp; `Window(n)` returns the last n consecutive minutes zero-filled ending at the latest minute (any line); `Window` empty when no timestamp or `n <= 0`; `Reset` clears
- [X] T002 [US1] Create `src/RemoteLogViewer/ErrorHistogram.cs`: `Add(LogLine, DateTime defaultDate)` (uses `LogTimestamp.TryParse`, skips `IsMarker`, count rule `IsEntry && Level == Error || IsAlert`), `Reset()`, `Window(int maxBars)` returning `(DateTime Minute, int Count)[]` (minute has zero seconds/ticks); pure, no WPF
- [X] T003 [US1] Create `src/RemoteLogViewer/ErrorStrip.cs`: `FrameworkElement`, `Height` 36, `BarWidth` 6 px, `Update(IReadOnlyList<(DateTime Minute, int Count)>)`, `MaxBars` = `ActualWidth / BarWidth` (raises `SizeChanged`-driven refresh request), `OnRender` draws bars scaled to the window's max (min 2 px for count > 0, Error red), `MouseMove` sets `ToolTip` to `"{minute:HH:mm} — {count}"` localized via `L`, `MouseLeftButtonUp` raises `MinuteClicked(DateTime)`; empty window draws nothing
- [X] T004 [US1] `src/RemoteLogViewer/LogView.xaml`: add `<local:ErrorStrip x:Name="Strip" DockPanel.Dock="Top" />` between the `WrapPanel` and the `ListBox`
- [X] T005 [US1] `src/RemoteLogViewer/LogView.xaml.cs`: field `ErrorHistogram _hist`; feed it in `Drain` per arriving line (UI thread, `Session.Date ?? DateTime.Today`) and after the `Older` insert; `ResetHistogram()` = `Reset()` + replay `_all` wherever `_counts` is recounted (trim, `older`) and in *Pulisci*; refresh `Strip.Update(_hist.Window(Strip.MaxBars))` once per `Drain` that changed anything and on `Strip.SizeChanged`; also refresh on `OnLanguageChanged`
- [X] T006 [US1] `src/RemoteLogViewer/LogView.xaml.cs`: handle `Strip.MinuteClicked` by extracting the select/scroll block of `AskTime` into a shared `GoTo(DateTime target, string label)` (`FindAtOrAfter` over `_visible`, same messages and Follow-off behaviour) and calling it with the minute start; `AskTime` keeps its dialog and calls the same helper
- [X] T007 [US1] `src/RemoteLogViewer/L.cs`: add English entries for any new Italian strings used by the strip tooltip (e.g. `"{0:HH:mm} — {1} errori"`)

**Checkpoint**: US1 works standalone.

## Phase 4: User Story 2 - Explain this line with AI (P1)

**Independent test**: right-click a middle line → *Spiega questa riga* opens the AI window with only that tab, the explain preset, and an input of ≤ 41 lines with the target marked `>>> `.

- [X] T008 [P] [US2] Tests in `tests/RemoteLogViewer.Tests/AiTests.cs`: `AiInput.Excerpt` middle (radius lines each side, target prefixed `>>> `), near start (shorter before), near end (shorter after), radius 0, single-line list; `AiPrompt.All()` contains the explain preset in `it` and `en` (set `L.Lang` and restore) and its text mentions the `>>>` marker
- [X] T009 [US2] `src/RemoteLogViewer/Ai.cs`: add `AiInput.Excerpt(IReadOnlyList<string> lines, int index, int radius) → string[]` and the explain preset (`"Spiega questa riga"`, text asking to explain the line marked `>>>` using the surrounding lines: meaning, likely cause, what to check next) at the end of `AiPrompt.All()`
- [X] T010 [US2] `src/RemoteLogViewer/AiWindow.xaml.cs`: add `Dictionary<LogView,string[]> _excerpts`; `public void Prefill(LogView view, string[] excerpt, string prompt)` = set override, `_selected` = only `view`, update the tab checkboxes (`_shown = []; RefreshTabs()`), `PromptBox.Text = prompt`, `FillPresets()`; in `Start_Click` source lines = `_excerpts.GetValueOrDefault(v) ?? v.VisibleTexts()`; clear `_excerpts` when the user toggles a tab checkbox and right after `AiInput.Build` in `Start_Click`
- [X] T011 [US2] `src/RemoteLogViewer/LogView.xaml(.cs)`: `ListBox.ContextMenu` with one `MenuItem` `{local:T 'Spiega questa riga'}`, built on `ContextMenuOpening` from the `ListBoxItem` under the mouse (cancel the menu when none, or the line is a marker/context separator); selecting it raises `public event Action<LogView, string[]>? ExplainRequested` with `AiInput.Excerpt(VisibleTexts(), index of that line in _visible, 20)`; set `AutomationProperties.Name`
- [X] T012 [US2] `src/RemoteLogViewer/MainWindow.xaml.cs`: wherever a `LogView` is created/opened, subscribe `ExplainRequested` (unsubscribe on close); handler opens or reuses `_ai` exactly like `Ai_Executed` (extract an `ShowAi()` helper returning the window) then calls `Prefill(view, excerpt, <explain preset text>)`
- [X] T013 [US2] `src/RemoteLogViewer/L.cs`: English for `Spiega questa riga` ("Explain this line") and the preset prompt text

**Checkpoint**: US1 and US2 both work.

## Phase 5: Polish & docs

- [X] T014 [P] `src/RemoteLogViewer/Guida.it.html` and `Guida.en.html`: histogram section and explain-line section (same section ids; shortcuts table unchanged since no new shortcut); run `GuideTests`
- [X] T015 [P] `README.md` and `README.it.md`: feature bullets
- [X] T016 [P] `CHANGELOG.md`: two entries under `## [Unreleased]` → `Added`
- [X] T017 `CLAUDE.md`: architecture notes (`ErrorHistogram`/`ErrorStrip` fed next to `_counts`, click via `GoTo`; `AiWindow.Prefill` + `AiInput.Excerpt` + `ExplainRequested`)
- [X] T018 `dotnet build` and `dotnet test` (kill a running `RemoteLogViewer.exe` first); then follow `quickstart.md` manually

## Dependencies

- T001 → T002 → T003 → T004 → T005 → T006; T007 any time after T003.
- T008 → T009 → T010 → T011 → T012; T013 after T009.
- US2 shares `LogView.xaml(.cs)` and `L.cs` with US1: do US1 first.
- Docs (T014–T017) after both stories; T018 last.

## Parallel opportunities

- T001 and T008 (different test files) can be written together.
- T014, T015, T016 touch different files.

## Implementation strategy

MVP = US1 (T001–T007), shippable alone. Then US2, then docs and verification.
