# Research: Custom levels, custom alerts and go to time

## Where patterns are evaluated

- **Decision**: in `LogLevels.Make`, on the session's background thread, with a per-session `LineRules` built once at `ActiveSession` construction.
- **Rationale**: level and alert become immutable `LogLine` data, like today; filters, counters, search and AI input follow without changes; no regex cost on the UI thread.
- **Alternatives**: evaluating in `LogView.Drain` (UI thread, 200 ms batches) — rejected, adds UI cost and duplicates level logic.

## Custom vs built-in levels

- **Decision**: custom patterns first, in severity order; fall back to built-in detection.
- **Rationale**: setting only an Error pattern must not lose WARN/INFO (spec scenario 1.3).
- **Alternatives**: custom patterns replace the built-in entirely — rejected for that reason.

## Invalid patterns at run time

- **Decision**: ignored (treated as empty) in `LineRules`; rejected at save time by `SessionConfig.Validate`.
- **Rationale**: import does not run session validation; a bad pattern must not stop tailing.

## Go to time input

- **Decision**: same time formats as the advanced search (`H:mm`, `H:mm:ss`), optionally preceded by `yyyy-MM-dd` or `dd/MM/yyyy`; time only → `Session.Date ?? today`.
- **Rationale**: consistency with `LogTimestamp` and `SearchWindow`.

## Dialog

- **Decision**: reuse `TextDialog` (validator callback), adding an optional empty-input message.
- **Alternatives**: a new dialog with date+time pickers — rejected, more code for no gain.
