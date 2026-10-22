# Feature Specification: Error histogram and "Explain this line" with AI

**Feature Branch**: `feature/005-error-histogram-explain-line`

**Created**: 2026-10-22

**Status**: Draft

**Input**: User description: "Two features: (1) Errors-per-minute histogram above the log view, clickable to jump to that minute. (2) 'Explain this line' with AI: from the context menu of a log line, open the AI analysis window pre-filled with that line and ±N lines of context. Reuse the existing AI input builder and runner, no new flow."

## Clarifications

### Session 2026-10-22

- Q: Which minutes does the histogram show for a long log? → A: One bar per minute for the most recent minutes that fit the strip width; no horizontal scroll, older minutes out of window.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Errors-per-minute histogram (Priority: P1)

A user watching a log tab sees a thin bar chart above the log list: one bar per minute, its height the number of error entries in that minute (alert matches count as errors, as for the badge). A burst of errors is visible at a glance. Clicking a bar scrolls the list to, and selects, the first line of that minute.

**Why this priority**: it turns "something broke around then" into a one-click navigation, with no need to scroll or type a time.

**Independent Test**: open a log with errors spread over several minutes with one spike; the spike bar is visibly the tallest; clicking it selects the first line of that minute.

**Acceptance Scenarios**:

1. **Given** a tab with error entries at 14:31 (2), 14:32 (9) and 14:35 (1), **When** the view is shown, **Then** bars for those minutes appear with heights proportional to 2/9/1 and the 14:32 bar is the tallest.
2. **Given** the histogram, **When** the user clicks the 14:32 bar, **Then** the list scrolls to and selects the first line whose timestamp is in 14:32 (Follow turns off).
3. **Given** the user hovers a bar, **Then** a tooltip shows the minute and the error count.
4. **Given** new error lines arrive while the tab is open, **Then** the histogram updates without user action.
5. **Given** the user clicks a minute whose lines are hidden by the current level/text filter, **Then** the view jumps to the nearest visible line at or after that minute (same behaviour as Go to time).
6. **Given** *Pulisci* is used, **Then** the histogram resets.

---

### User Story 2 - Explain this line with AI (Priority: P1)

From the context menu of a log line, the user picks *Spiega questa riga / Explain this line*. The AI analysis window opens with the log tab pre-selected and the input pre-filled with the chosen line plus N lines of context before and after, and an "explain this line" prompt. The user reviews and starts the analysis as usual.

**Why this priority**: it removes the copy/paste step between a suspicious line and the AI tool.

**Independent Test**: right-click a line in the middle of a log, choose the command: the AI window opens showing that line and its neighbours as the input, with the explain prompt selected; starting it runs the configured tool.

**Acceptance Scenarios**:

1. **Given** a selected or right-clicked line, **When** the user chooses the command, **Then** the AI window opens (or is brought to front) with only that tab included and the input limited to the line and ±N context lines (marking the target line).
2. **Given** the line is within N lines of the buffer start or end, **Then** the context is simply shorter.
3. **Given** the AI window is already open, **Then** it is reused and re-filled, not duplicated.
4. **Given** the user has not yet consented to sending data to the selected tool, **Then** the usual consent flow applies before anything is sent.
5. **Given** the filter hides some lines, **Then** the context is taken from the visible lines (same as the normal AI input).
6. **Given** the language is switched, **Then** the menu entry and prompt follow it.

---

### Edge Cases

- Lines without a timestamp inherit the previous line's minute (as in search); lines before any timestamp are not counted.
- Logs spanning many hours/days: the histogram shows only the most recent minutes that fit the width (see FR-006); an empty histogram (no errors) shows an empty strip, not an error.
- Marker lines and context separators are never counted or used as targets.
- Buffer trim or prepending older lines keeps the histogram consistent with the buffer.
- Clicking a bar whose lines were trimmed does nothing harmful (message or no-op).
- Right-click on empty space or a separator offers no "Explain" entry.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Each log tab MUST show a histogram strip above the list with one bar per minute, height proportional to the number of error entries in that minute.
- **FR-002**: Error count MUST follow the same rule as the unread-error badge (Error-level entries and custom-alert matches; a stack trace counts once per entry; initial history lines are included in the histogram).
- **FR-003**: Clicking a bar MUST select the first line of that minute and scroll it into view, turning Follow off.
- **FR-004**: Bars MUST show a tooltip with minute and count.
- **FR-005**: The histogram MUST update as lines arrive and after clear, trim and load-older.
- **FR-006**: The histogram MUST show one bar per minute for as many most-recent minutes as fit the strip width, with no horizontal scrolling; older minutes are out of the window.
- **FR-007**: The log line context menu MUST include *Spiega questa riga* (it) / *Explain this line* (en).
- **FR-008**: The command MUST open the existing AI window pre-filled with the line and ±N context lines, restricted to the line's tab, with an explain prompt preselected.
- **FR-009**: The feature MUST reuse the existing AI input building and execution; no separate execution path or window.
- **FR-010**: N MUST have a sensible default (assumption: 20) without a new setting.
- **FR-011**: All new UI strings MUST be localized (it/en); user guides, READMEs and CHANGELOG MUST be updated.

### Key Entities

- **Minute bucket**: a minute and the error count in it, derived from the buffer.
- **Explain request**: tab, target line and context radius passed to the AI window.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can locate an error burst and jump to it in one click, under 5 seconds from looking at the tab.
- **SC-002**: The histogram stays responsive (no visible lag while scrolling or on the 200 ms updates) with a full 100,000-line buffer.
- **SC-003**: From right-click to a running explanation takes the command plus one Start click.
- **SC-004**: Histogram counts match the error counters for the same minutes in 100% of tested cases.

## Assumptions

- Minute is taken from the line's own timestamp (same parsing as search and Go to time); date-less times use the session date or today.
- A gap with no errors still takes one (empty) bar slot, so bar position maps linearly to time.
- Only error-level entries and alert matches are charted; other levels are out of scope.
- N=20 context lines each side; not configurable in this feature.
- The AI window and its consent, tool selection and report features are unchanged.
- No new external dependencies; no persistence of histogram data.
