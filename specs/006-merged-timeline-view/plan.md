# Implementation Plan: Merged timeline view

**Branch**: `feature/006-merged-timeline-view` | **Date**: 2026-10-23 | **Spec**: [spec.md](spec.md)

## Summary

A new tab type, `MergedView`, that shows the lines of all open session tabs interleaved by timestamp, with a coloured session column. The ordering logic is a pure, tested class `TimelineMerge` (incremental sorted insert, timestamp inheritance per session, deterministic ties). It starts from `LogView.Snapshot()` of every session tab (as `LogSearch` does) and then stays live through two new `LogView` events (`LinesAppended`, `LinesPrepended`) plus `Closed`. Filtering reuses `ContextFilter`; the toolbar is a reduced copy of the `LogView` one. `MainWindow` learns that a tab's `Tag` may be a `MergedView`.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0-windows`), WPF

**Primary Dependencies**: none new (no NuGet)

**Storage**: none (the merged tab is not saved in the workspace, not exported)

**Testing**: xUnit (`dotnet test`); `TimelineMerge` pure and tested, UI checked manually (quickstart)

**Target Platform**: Windows desktop

**Project Type**: desktop-app

**Performance Goals**: append in order O(new lines) per 200 ms tick; out-of-order insert O(log n) search + O(n) list shift; full rebuild of the visible list O(n) ≤ ~150 ms at 200k rows

**Constraints**: UI thread only (`_all` of each `LogView` is not thread-safe, the events are raised from `Drain`); merged buffer capped at 200,000 rows (trim in blocks of 20,000 like `LogView`); `LogLine` stays a class and source `LogLine`s are never mutated by the merged view (`ContextFilter` sets `IsContext`)

**Scale/Scope**: up to ~8 sessions, ≤ 100,000 lines each at open, 200,000 rows retained

## Constitution Check

`.specify/memory/constitution.md` is the unfilled template: no gates. Project rules from `CLAUDE.md` apply: localization via `L` (English in `En`), `LogLine` stays a class, `Checked`/`Unchecked` not `Click` for toggles with `IsLoaded` guards, docs/CHANGELOG/guides updated. Re-check after design: no violations.

## Project Structure

### Documentation (this feature)

```text
specs/006-merged-timeline-view/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
└── tasks.md             # /speckit-tasks
```

### Source Code (repository root)

```text
src/RemoteLogViewer/
├── TimelineMerge.cs       # NEW pure: MergedLine, MergedSession, TimelineMerge (Add/Trim/Rows)
├── MergedView.xaml(.cs)   # NEW UserControl: toolbar, session checkboxes, ListBox with session column
├── LogView.xaml.cs        # LinesAppended / LinesPrepended / Closed events raised from Drain / Close
├── MainWindow.xaml(.cs)   # Merge RoutedCommand (toolbar + Ctrl+Shift+M), merged tab, Tag-type handling
├── L.cs                   # en strings
├── Guida.it.html / Guida.en.html
tests/RemoteLogViewer.Tests/
└── TimelineMergeTests.cs  # NEW
README.md, README.it.md, CHANGELOG.md, CLAUDE.md
```

**Structure Decision**: single WPF project. Everything that can be decided without WPF (ordering, inheritance, ties, trimming) lives in `TimelineMerge`; `MergedView` is glue.

## Design notes

- **Merge model.** `MergedLine : LogLine` is a *copy* of a source line (same text/level/flags) plus `Original` (the source `LogLine`, for Reveal), `Session` (`MergedSession`), `Time` (effective timestamp) and `Seq` (per-session order). A copy, not the source object, because `ContextFilter` mutates `IsContext` and `LogLine` objects are shared with the source tab. Sort key = `(Time, Session.Index, Seq)`: lines of one session keep their order, ties across sessions are stable (FR-005).
- **Effective time.** Per session `TimelineMerge` keeps `Last`. A line with its own timestamp (`LogTimestamp.TryParse(text, defaultDate)`, `defaultDate = Session.Date ?? today`, taken when the view opens) sets `Last`; a line without one, and marker lines, inherit `Last`. Before the first timestamp `Last = DateTime.MinValue`, so those lines sort first, in arrival order; `MergedSession.HasTimestamps` becomes true on the first parsed timestamp and the view shows a note for a session that has none (edge case, never dropped).
- **Start and live.** On open, `MainWindow` takes `Snapshot()` of each `LogView` tab (UI thread) and subscribes to its events in the same dispatcher turn (no gap). `LogView.Drain` raises `LinesAppended(IReadOnlyList<LogLine>)` with the lines dequeued from `Pending` in that tick and `LinesPrepended(IReadOnlyList<LogLine>)` for each `Older` block. *Pulisci* and the 100k trim raise nothing: the merged rows stay (spec US2-4). `LogView.Close()` raises `Closed`.
- **Insert.** `Add(session, lines, prepend)` converts to `MergedLine`s, sorts the batch and returns whether it landed entirely after the current last row. If yes the view just feeds the new visible lines to its `ContextFilter` (cheap path, same as `LogView.Drain`). Otherwise (a late line, a prepend, or the initial snapshot of the 2nd+ session) the batch is merged with `Rows` in one linear pass, never one `List.Insert` per line (that would be ~10^10 element moves for two 100k-line snapshots, against SC-001), and the view calls `Rebuild(keepTopLine: true)` (ContextFilter is index-based). `// ponytail:` the merge and rebuild are O(n) per out-of-order tick; incremental visible insert if profiling shows it.
- **Prepend.** `Older` blocks get `Seq` values below the session's current minimum, preserving their order. The leading no-timestamp rows of the old head (which had `MinValue`) are re-timed to the prepended block's last effective time and re-sorted, so no row stays misplaced (SC-002).
- **Cap.** When rows exceed `MaxRows` = 200,000, the oldest `TrimBlock` = 20,000 rows are removed and the view rebuilds.
- **Closed source.** `Closed` marks the `MergedSession` closed: name shown with a suffix, "go to source" disabled for its rows, no more events arrive. Rows stay (clarification Q2).
- **View.** `MergedView` toolbar: *Follow*, *A capo*, *Salva…*, filter text + *Regex* + *Contesto*, level checkboxes (counts of entries per level), and one checkbox per session (coloured swatch, name; unchecking hides its rows = "included" choice, FR-002/FR-009). Each row: fixed-width coloured session cell + line text; level colours, context dimming and markers as in `LogView`. Session colour from a fixed 8-colour palette by session index, readable on the light background. Filter predicate = level AND session visible AND text/regex, evaluated by `ContextFilter` (levelOk gets the session test too). Follow turns off on user scroll-up, as `Lines_ScrollChanged`. `Ctrl+S` saves `visible` rows as `[session] text`, UTF-8 without BOM. `Ctrl+C` copies the selected rows' text. Row context menu / double-click → `RevealRequested(LogView, LogLine)`, which `MainWindow` maps to the existing `RevealLine` (selects the tab unless Affianca; `NotFound` → message that the line is no longer in the buffer). The merged view raises no alerts and has no tab badge (FR-012).
- **MainWindow.** `Merge` `RoutedCommand` (toolbar *Unisci…*, `Ctrl+Shift+M`): needs ≥ 2 session tabs (else an info message); opens one merged tab (a second invocation selects the existing one and says new sessions are not added — reopen to include them). Tab header: coloured dot-less title "Unione (n)", close button, middle-click close, drag reorder reuse the tab code via a small `AddTab(TabItem)` split out of `Open`. Everywhere `(LogView)t.Tag` is assumed (`OpenFiles`, `LocalConfig`, search and AI sources, workspace save) switches to `t.Tag as LogView` / `OfType<LogView>()`; `Relayout` and `CloseTab` handle `MergedView` (side-by-side title "Unione"). Workspace: merged tab is not saved; `Selected` is computed among session tabs only.
- **No dialog.** FR-002's "choose which sessions" is the per-session checkboxes inside the view (all on by default); a pre-open picker is not worth a dialog. Spec FR-002 reworded accordingly.
- **i18n.** Italian keys in code/XAML, English added to `L.En`; guide sections identical ids in both languages (`merged`), shortcut table row `Ctrl+Maiusc+M`.

## Complexity Tracking

No violations.
