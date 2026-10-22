# Implementation Plan: Error histogram and "Explain this line" with AI

**Branch**: `feature/005-error-histogram-explain-line` | **Date**: 2026-10-22 | **Spec**: [spec.md](spec.md)

## Summary

Two additions to the log tab. (1) `ErrorHistogram`: a pure, incremental per-minute error counter fed by `LogView` next to `_counts`, drawn by a small `ErrorStrip` element above the list; a bar click reuses the Go-to-time lookup (`LogTimestamp.FindAtOrAfter` over `_visible`). (2) A `ListBox` context menu entry raises an event that `MainWindow` forwards to the existing singleton `AiWindow`, which gets a `Prefill` entry point: only that tab selected, an excerpt (`AiInput.Excerpt`: ±20 visible lines, target marked) used instead of `VisibleTexts()`, and a new "explain this line" preset in `AiPrompt.All()`. Execution, consent, output and reports are untouched.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0-windows`), WPF

**Primary Dependencies**: none new (no NuGet)

**Storage**: none (histogram is in memory; `ai.json` unchanged)

**Testing**: xUnit (`dotnet test`), pure classes tested; UI checked manually (quickstart)

**Target Platform**: Windows desktop

**Project Type**: desktop-app

**Performance Goals**: histogram update O(new lines) per 200 ms tick; full recompute (trim / load older / clear) O(n) ≤ ~100 ms at 100k lines

**Constraints**: UI thread only for `_all`; no horizontal scroll; no new settings

**Scale/Scope**: buffer ≤ 100,000 lines; strip ≤ a few hundred bars

## Constitution Check

`.specify/memory/constitution.md` is the unfilled template: no gates. Project rules from `CLAUDE.md` apply (localization via `L`, docs/CHANGELOG updates, `LogLine` stays a class). Re-check after design: no violations.

## Project Structure

### Documentation (this feature)

```text
specs/005-error-histogram-explain-line/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
└── tasks.md             # /speckit-tasks
```

### Source Code (repository root)

```text
src/RemoteLogViewer/
├── ErrorHistogram.cs        # NEW pure: Add/Reset, Window(maxBars)
├── ErrorStrip.cs            # NEW FrameworkElement: draws bars, tooltip, Clicked(minute)
├── LogView.xaml(.cs)        # strip above the list; histogram feed; context menu; ExplainRequested
├── Ai.cs                    # AiInput.Excerpt (pure), explain preset in AiPrompt.All()
├── AiWindow.xaml.cs         # Prefill(view, excerpt, prompt); Start uses the excerpt override
├── MainWindow.xaml.cs       # wires LogView.ExplainRequested -> open/reuse AiWindow -> Prefill
├── L.cs                     # en strings
├── Guida.it.html / Guida.en.html
tests/RemoteLogViewer.Tests/
├── ErrorHistogramTests.cs   # NEW
└── AiTests.cs               # Excerpt + preset cases (existing)
README.md, README.it.md, CHANGELOG.md, CLAUDE.md
```

**Structure Decision**: single WPF project; logic that can be pure (`ErrorHistogram`, `AiInput.Excerpt`) is pure and tested, WPF glue stays thin.

## Design notes

- **Histogram feed.** `ErrorHistogram` keeps `Dictionary<DateTime,int>` (minute → error entries) plus the last seen minute (lines without a timestamp inherit it; markers skipped; nothing counted before the first timestamp). A line counts when `IsEntry && Level == Error` or `IsAlert`, history included (FR-002). `LogView.Drain` calls `Add` per arriving line; `Reset()` + replay of `_all` after trim, prepend of `Older` and *Pulisci*, exactly where `_counts` is recounted.
- **Series (revised).** `Series()` returns the consecutive minutes from the first error minute to the latest minute seen, empty minutes included, capped to the last `MaxMinutes` = 50,000. `ErrorStrip` is as wide as the series (6 px per bar) inside a horizontal `ScrollViewer` that stays at the right end unless the user scrolled back. A `HistButton` toggle collapses the host.
- **Click.** `ErrorStrip.MinuteClicked(DateTime)` → `LogView` calls `FindAtOrAfter(_visible, minute, day)` and selects/scrolls like `AskTime` (Follow off); same filter-hidden behaviour as Go to time (scenario 5). A minute with no visible line at/after it shows the existing "no visible line" message.
- **Day.** Parsing default date = `Session.Date ?? DateTime.Today`, as in Go to time.
- **Explain.** `ListBox.ContextMenu` built on `ContextMenuOpening` from the item under the mouse (no item/marker → no entry, menu cancelled). `LogView.ExplainRequested(LogView, LogLine)`; `MainWindow` opens/reuses `_ai` (as `Ai_Executed`) and calls `Prefill`. `Excerpt(lines, index, 20)` returns ≤ 41 texts from the visible lines, target prefixed `>>> `; the explain preset says the marked line is the one to explain. In `AiWindow`, a `Dictionary<LogView,string[]>` override feeds `AiSource` in `Start_Click`; it is cleared when the user changes the tab checkboxes or after the run starts, so later normal runs use full visible lines.
- **i18n.** Italian keys in code/XAML, English added to `L.En`.

## Complexity Tracking

No violations.
