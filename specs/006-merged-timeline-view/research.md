# Research

No external unknowns (no new dependency). Decisions taken from reading the existing code.

## Decision 1: copy source lines into `MergedLine` instead of reusing `LogLine`

- **Decision**: `MergedLine : LogLine` holds a copy plus `Original`.
- **Rationale**: `ContextFilter.Show` writes `LogLine.IsContext`; the same objects are displayed in the source tab, so sharing them would dim/undim lines in the wrong tab. `LogLine` is not sealed and its properties are get-only from the constructor, so a subclass is cheap. Identity of `Original` is what `LogView.Reveal` needs.
- **Alternatives**: reuse the source object (rejected: shared mutable `IsContext`); wrap in a view-model not deriving from `LogLine` (rejected: `ContextFilter` and the `ListBox` templates are typed on `LogLine`).

## Decision 2: stay live through events, snapshot only at open

- **Decision**: `LogView` raises `LinesAppended`/`LinesPrepended`/`Closed`; the merged view takes one `Snapshot()` per session at open.
- **Rationale**: re-snapshotting 100k lines per session every 200 ms would be wasteful. Both snapshot and event subscription happen on the UI thread in one turn, so no line is lost or doubled.
- **Alternatives**: poll `Snapshot()` and diff by count (rejected: breaks on trim/prepend); drain `Session.Pending` directly (rejected: the queue is consumed by the source `LogView`).

## Decision 3: sort key `(Time, SessionIndex, Seq)`

- **Rationale**: total order, stable across refreshes (FR-005), keeps per-session order for equal timestamps. Timestamp-less lines inherit, so a stack trace stays glued to its head line.
- **Alternatives**: stable merge by arrival time (rejected: depends on poll timing, not reproducible); merge by tick + timestamp (rejected: more state, no gain).

## Decision 4: rebuild on out-of-order insert

- **Rationale**: `ContextFilter` is index-based (see `CLAUDE.md`), so an insert in the middle shifts indices. The common case (everything in order) stays incremental. Cost is bounded by the 200k row cap.
- **Alternatives**: incremental visible insert with context recompute (rejected: complexity, no measured need).

## Decision 5: tab, not window; no picker dialog

- Tab per clarification Q1. Session selection is inside the view (checkboxes) to avoid an extra dialog; sessions opened after the merged tab was created are not added (documented), reopening picks them up.

## Decision 6: unbounded growth guard

- Source buffers trim at 100k, but merged rows are kept after a source trim/clear (spec). A 200,000-row cap with block trim bounds memory; the oldest rows drop first.
