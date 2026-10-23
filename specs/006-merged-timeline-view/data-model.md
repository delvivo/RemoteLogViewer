# Data Model

In-memory only; nothing persisted or exported.

## MergedSession

- `Index` (int, order of the source tab at creation), `Name` (display name, `Session.DisplayName` at creation), `Color` (from the fixed palette by `Index`), `DefaultDate` (`Session.Date ?? today` at creation).
- `Source` (`object?`, the source `LogView`; null once closed; typed `object` to keep the merge free of WPF types), `Closed` (bool), `Visible` (bool, the view checkbox), `HasTimestamps` (bool, set when a line parses).
- Internal for the merge: `Last` (last effective time, starts `DateTime.MinValue`), `NextSeq`, `MinSeq`.

## MergedLine : LogLine

- Copy of `Text`, `Level`, `IsMarker`, `IsEntry`, `IsHistory`, `IsAlert` of the source line.
- `Original` (source `LogLine`), `Session` (`MergedSession`), `Time` (effective `DateTime`), `Seq` (long, per session, negative for prepended blocks).
- Ordering: `(Time, Session.Index, Seq)` ascending.

## TimelineMerge (pure)

- `MergedSession AddSession(string name, DateTime defaultDate, LogView? source = null)`
- `bool Add(MergedSession s, IReadOnlyList<LogLine> lines, bool prepend = false)`: converts and inserts; returns `true` when every new row went after the previous last row (append-only: no existing index shifted).
- `IReadOnlyList<MergedLine> Rows` (sorted).
- `bool Trim()`: when `Rows.Count > MaxRows` (200,000) removes the oldest `TrimBlock` (20,000) rows, returns `true` if it did.
- Invariants: `Rows` always sorted by the key; rows of one session appear in `Seq` order; no row is lost on insert; `Rows.Count` = lines fed − trimmed.

## Events added to LogView

- `LinesAppended(IReadOnlyList<LogLine>)`: lines dequeued from `Pending` in one `Drain`, in arrival order.
- `LinesPrepended(IReadOnlyList<LogLine>)`: one `Older` block, in file order.
- `Closed()`: raised from `Close()`.

## Merged tab (MainWindow)

- `TabItem.Tag = MergedView`; not part of `Workspace`; at most one at a time.
