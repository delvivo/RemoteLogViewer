# Feature Specification: Custom levels, custom alerts and go to time

**Feature Branch**: `feature/004-levels-alerts-goto`

**Created**: 2026-10-22

**Status**: Draft

**Input**: User description: "Three usability features for log tailing: (1) Per-session configurable level detection: optional regex override per level (Error, Warn, Info, Debug) in the session config, so custom formats like "[E]", "|ERR|", "level=50" are classified; when empty, the current built-in detection applies. Filters, counters, badges and alerts follow automatically. (2) Custom alerts: a per-session regex (e.g. "OutOfMemory|deadlock|timeout"); new (non-history) lines matching it raise the same unread badge and taskbar flash as errors. No Windows toast. (3) Go to time (Ctrl+G) in a log tab: user enters a time (e.g. 14:32 or a date-time), the view jumps to and selects the first line whose timestamp is >= that time (lines without timestamp inherit the previous one, as in advanced search); if the time is earlier than the buffer and older lines can be loaded, tell the user. UI localized it/en; guides and READMEs updated."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Custom level patterns per session (Priority: P1)

A user tails a log whose format marks levels in a way the app does not recognise (e.g. `[E]`, `|ERR|`, `level=50`). In the session's settings they enter a pattern for each level they care about. From then on, when the session is opened, those lines are classified with the right level: level checkboxes filter them, counters count them, error badges and taskbar flashing react to them.

**Why this priority**: without correct levels every other level-based feature (filters, counters, alerts, search by level, AI error report input) is useless for that log.

**Independent Test**: create a session on a file containing `[E] boom` and `[I] ok`, set Error = `\[E\]`, Info = `\[I\]`, open it: the first line counts as Error, the second as Info, unchecking Error hides the first.

**Acceptance Scenarios**:

1. **Given** a session with Error pattern `\|ERR\|`, **When** a line `2026-10-22 |ERR| failed` is read, **Then** it is classified as Error and counted in the Error counter.
2. **Given** a session with no custom patterns, **When** lines are read, **Then** classification is exactly the current built-in one.
3. **Given** a session with only an Error pattern set, **When** a line contains `WARN` and does not match the Error pattern, **Then** the built-in detection still classifies it as Warn.
4. **Given** the user types an invalid pattern in the session settings, **When** they try to save, **Then** the save is refused with a message naming the invalid field.
5. **Given** a line with no level that follows an Error line, **When** it is read, **Then** it inherits Error as today (stack-trace behaviour unchanged).

---

### User Story 2 - Custom alert pattern per session (Priority: P2)

A user wants to be warned not only about errors but about specific text (e.g. `OutOfMemory|deadlock|timeout`), whatever its level. They set an alert pattern on the session. When a new line matching it arrives while the tab is not visible, the tab's unread badge grows and, if the window is not active, the taskbar flashes — exactly like errors today.

**Why this priority**: extends an existing, valued mechanism to the cases the level alone misses.

**Independent Test**: session with alert pattern `deadlock`; with another tab selected, append `INFO deadlock detected` to the file: the session's tab shows an unread badge and the taskbar flashes if the window is inactive.

**Acceptance Scenarios**:

1. **Given** alert pattern `timeout` and the tab not visible, **When** a new `INFO request timeout` line arrives, **Then** the unread badge increases by 1.
2. **Given** alert pattern `timeout`, **When** the matching line is part of the initial tail or of loaded previous lines, **Then** no alert is raised.
3. **Given** a new Error line that also matches the alert pattern, **When** it arrives, **Then** it raises one alert, not two.
4. **Given** the alert pattern is empty, **When** lines arrive, **Then** alerting behaves exactly as today.
5. **Given** an invalid alert pattern, **When** the user saves the session, **Then** the save is refused with a message.

---

### User Story 3 - Go to time (Priority: P3)

While reading a long log, the user wants to see what happened at 14:32. They press `Ctrl+G` (or a toolbar/tab button), type `14:32` (or a date and time), confirm, and the view scrolls to and selects the first visible line whose timestamp is at or after that moment. Follow mode turns off so the view stays there.

**Why this priority**: big time saver on large buffers, but the app is usable without it.

**Independent Test**: open a local file with lines at 10:00, 14:30, 14:35; go to `14:32`: the 14:35 line is selected and in view.

**Acceptance Scenarios**:

1. **Given** lines at 10:00, 14:30 and 14:35, **When** the user goes to `14:32`, **Then** the 14:35 line is selected and scrolled into view, and Follow is off.
2. **Given** a stack trace without timestamps after a 14:35 line, **When** the user goes to `14:35`, **Then** the 14:35 line (first in the block) is selected.
3. **Given** all lines are before the requested time, **When** the user confirms, **Then** the user is told that no line is at or after that time and the view does not move.
4. **Given** the requested time is earlier than the oldest line in the buffer and previous lines can still be loaded, **When** the user confirms, **Then** the first line is selected and the user is told that earlier lines can be loaded with the existing "load previous lines" action.
5. **Given** an input that is not a valid time or date-time, **When** the user confirms, **Then** an error is shown and the dialog stays open.
6. **Given** no line in the buffer has a recognisable timestamp, **When** the user confirms, **Then** the user is told that the log has no timestamps.

---

### Edge Cases

- Custom patterns overlap (a line matches both Error and Warn patterns): the most severe level wins (Error > Warn > Info > Debug).
- A pattern that takes too long to evaluate on a pathological line must not freeze the app: evaluation is time-limited and a timed-out match counts as no match.
- Session settings edited while the session is open: the running tab keeps its old settings; the new ones apply when the session is reopened (consistent with all other session settings).
- Local files opened from disk have no session settings: built-in levels, no custom alert.
- Export/import and duplicate of sessions carry the new settings (they are not secrets).
- Go to time with filters active: only lines currently visible are considered.
- Go to time with a time only: the date is the session's date (dated path) or today, as in advanced search.
- Pattern matching is case-insensitive, like the existing text filter.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The session settings MUST offer four optional patterns, one each for Error, Warn, Info and Debug.
- **FR-002**: When at least one custom pattern is set, a line MUST be checked against the custom patterns in severity order (Error, Warn, Info, Debug); the first match sets the level. If no custom pattern matches, the built-in detection MUST apply.
- **FR-003**: When no custom pattern is set, level detection MUST be identical to the current behaviour.
- **FR-004**: Lines without a detected level MUST keep inheriting the previous line's level, whatever the detection source.
- **FR-005**: The session settings MUST offer one optional alert pattern.
- **FR-006**: A new line (not part of the initial tail and not loaded as previous lines) that matches the alert pattern MUST raise an alert identical to an error alert (unread badge while the tab is not visible, taskbar flash while the window is inactive). A line that is both an error and an alert match MUST count once.
- **FR-007**: Invalid patterns MUST be rejected when saving the session, with a localized message naming the field.
- **FR-008**: Pattern matching MUST be case-insensitive and time-limited.
- **FR-009**: The new settings MUST be persisted with the session and included in export, import and duplicate.
- **FR-010**: Each log tab MUST offer "Go to time" via `Ctrl+G` and a visible control.
- **FR-011**: Go to time MUST accept the same time formats as the advanced search time range (time only, or date and time).
- **FR-012**: Go to time MUST select and scroll to the first visible line whose timestamp (own, or inherited from the previous timestamped line) is at or after the requested moment, and turn Follow off.
- **FR-013**: Go to time MUST inform the user when no line qualifies, when the log has no timestamps, and when earlier lines can still be loaded.
- **FR-014**: All new UI text MUST be available in Italian and English; the user guides (it/en) and READMEs (en/it) MUST document the three features and the new shortcut.

### Key Entities

- **Session settings** (existing): gains four optional level patterns and one optional alert pattern.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A log in a non-standard format can be fully classified by entering at most four patterns, with no other change.
- **SC-002**: Sessions saved before this feature open and behave exactly as before.
- **SC-003**: A matching alert line produces a badge within the normal refresh delay (under 1 second from the line being read).
- **SC-004**: Go to time on a full buffer (100,000 lines) positions the view in under 1 second.
- **SC-005**: Every new string appears translated when the UI language is switched.

## Assumptions

- Custom patterns complement the built-in detection (fallback) instead of replacing it: setting only an Error pattern must not lose Warn/Info detection.
- "Severity order wins" is the expected rule for overlapping patterns.
- No Windows toast notification (explicit user choice).
- Alerts reuse the existing unread counter: the badge counts errors and custom alerts together.
- Go to time works on the lines currently in the buffer; it never loads previous lines by itself.
