# Research: Error histogram and Explain this line

No NEEDS CLARIFICATION remained. Decisions:

## Histogram storage: incremental dictionary

- **Decision**: `Dictionary<DateTime,int>` fed per line in `Drain`; full replay only where `_counts` is already recounted (trim, `Older`, clear).
- **Rationale**: re-scanning 100k lines with the timestamp regex every 200 ms would cost far more than parsing only arriving lines.
- **Alternatives**: recompute from `_all` each tick (too slow); cache the minute on `LogLine` (touches a class used by search/UI; `LogLine` also needs a default date at parse time).

## Rendering: custom `FrameworkElement` (`OnRender`)

- **Decision**: one element drawing rectangles with `DrawingContext`, hit-testing by `x / BarWidth`.
- **Rationale**: no virtualization or per-bar visual overhead; mouse and tooltip trivial; no NuGet chart library (project has none).
- **Alternatives**: `ItemsControl` of `Rectangle`s (hundreds of visuals rebuilt every tick), a charting library (new dependency).

## Click navigation: reuse Go to time

- **Decision**: `LogTimestamp.FindAtOrAfter` on `_visible` with the minute start.
- **Rationale**: already tested; gives the scenario-5 behaviour for filtered views for free.

## Explain flow: prefill the existing `AiWindow`

- **Decision**: `Prefill` overrides the lines of one tab for the next run; excerpt from visible lines with the target marked `>>> `; new preset in `AiPrompt.All()`.
- **Rationale**: spec forbids a new flow; `AiInput.Build` already takes `AiSource` with arbitrary lines, so only the source lines change. Consent, runner, report untouched.
- **Alternatives**: separate "explain" window (duplicates runner UI); passing the line through the prompt text (loses the `=== name (path) ===` structure and the 1 MB budget logic).
- **Known trade-off**: starting an explain run stores its prompt as `LastPrompt` (same as any run).
