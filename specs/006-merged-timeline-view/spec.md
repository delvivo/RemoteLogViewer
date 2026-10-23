# Feature Specification: Merged timeline view

**Feature Branch**: `feature/006-merged-timeline-view`

**Created**: 2026-10-23

**Status**: Draft

**Input**: User description: "Merged view by timestamp. Several tabs interleaved in chronological order in a single view, with a coloured session column. Needed to correlate several services, which Affianca (side-by-side) does not solve. It can start from the snapshots, as LogSearch does."

## Clarifications

### Session 2026-10-23

- Q: Where does the merged view open? → A: As a tab in the main window (can be shown side by side with session tabs), not a separate window.
- Q: What happens to a session's rows when its source tab is closed? → A: Rows stay, the session is marked closed, receives no new lines, and "go to source tab" is disabled for its rows.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Chronological merge of selected sessions (Priority: P1)

A user debugging a request that crosses several services has one tab per service. They open the merged view, choose which open tabs to include, and see a single list where the lines of all chosen sessions are interleaved in timestamp order. Each row has a session column, coloured per session, so the origin of every line is clear at a glance.

**Why this priority**: it is the whole point of the feature: seeing "service A logged X, then service B logged Y 20 ms later" without comparing separate scrolling panes.

**Independent Test**: open two tabs whose lines interleave in time (A 10:00:01, B 10:00:02, A 10:00:03); the merged view lists them in that order, each with its own session name and colour.

**Acceptance Scenarios**:

1. **Given** two open tabs with lines at 10:00:01 / 10:00:03 (A) and 10:00:02 (B), **When** the user opens the merged view with both selected, **Then** the rows appear in the order A, B, A with the session column showing A, B, A in two distinct colours.
2. **Given** a line without its own timestamp (e.g. a stack-trace line) following a timestamped line in session A, **When** merged, **Then** it stays right after that line, not moved elsewhere by the other session's lines.
3. **Given** three open tabs, **When** the user selects only two, **Then** only those two appear in the merged view.
4. **Given** two lines with the same timestamp from different sessions, **When** merged, **Then** the order is stable (same on every refresh; lines of one session keep their original relative order).

---

### User Story 2 - Live updates (Priority: P2)

While the merged view is open, new lines arriving in the included sessions show up in the correct chronological position, and the view can follow the bottom like a normal tab.

**Why this priority**: the app is a live tail; a frozen snapshot would be useful but half of the value.

**Independent Test**: with the merged view open, append lines to both source files; they appear in the view in timestamp order, without reopening it.

**Acceptance Scenarios**:

1. **Given** the merged view in Follow mode, **When** new lines arrive in either session, **Then** they are added in timestamp order and the view stays at the bottom.
2. **Given** a late line (timestamp older than lines already shown) arriving from one session, **When** it is merged, **Then** it is inserted at its chronological place, not appended at the end.
3. **Given** a source tab is closed, **When** the merged view refreshes, **Then** that session's rows stay, the session is marked as closed and receives no new lines, and "go to source tab" is disabled for its rows, without errors.
4. **Given** a source tab is cleared (*Pulisci*), **When** the merged view refreshes, **Then** lines already merged are kept and only new lines are added.

---

### User Story 3 - Filter and navigate the merged view (Priority: P3)

The user can narrow the merged view with the same tools as a normal tab: level filters, text/regex filter with optional context, and can hide or show individual sessions from it. Selecting a row can jump back to that line in its original tab.

**Why this priority**: useful once the merge exists; the merge alone is already valuable.

**Independent Test**: hide session B from the merged view; only A rows remain; apply an Error-only level filter; only error rows of A remain.

**Acceptance Scenarios**:

1. **Given** the merged view of A and B, **When** the user unchecks B, **Then** only A's rows remain and re-checking B restores them in order.
2. **Given** a level or text filter, **When** applied, **Then** rows are filtered across all sessions consistently with how a single tab filters.
3. **Given** a row in the merged view, **When** the user asks to open it in its source tab, **Then** that tab is selected and the line revealed (as search results do).

---

### Edge Cases

- A session whose lines have no timestamp at all: its lines cannot be placed; they are grouped at the start in arrival order and the user is told the session has no timestamps (never silently dropped).
- Different date formats across sessions (ISO, `dd/MM/yyyy`, time only): time-only timestamps use the session's date (or today), as in cross-tab search.
- Clock skew between servers: ordering follows the timestamps as written; no correction.
- Sessions with a very large number of lines: the merged view stays responsive; the same buffer limits as a single tab apply per session.
- Fewer than two open tabs: the command explains that merging needs at least two sessions.
- Lines in a session whose timestamps go backwards (rotation, unsorted file): ordering uses the line's effective timestamp; no crash.
- Marker lines (day change) and history lines from the initial read are merged like any other line.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Users MUST be able to open a merged view from the main window (toolbar command and keyboard shortcut) that combines the lines of chosen open sessions. It opens as a tab in the main window, closable, reorderable and usable in side-by-side mode like any other tab.
- **FR-002**: Users MUST be able to choose which open sessions are included, with all open sessions included by default, by showing or hiding each session inside the merged view (sessions opened after the merged view was created are not added; reopening it includes them).
- **FR-003**: The merged view MUST list lines in ascending order of their timestamp across all included sessions.
- **FR-004**: A line without its own timestamp MUST take the timestamp of the previous line of the same session, so it stays attached to it.
- **FR-005**: Ties MUST be resolved deterministically, preserving each session's own line order.
- **FR-006**: Each row MUST show a session column with the session name, coloured with a colour unique to that session within the view; the line itself keeps its level colouring.
- **FR-007**: The merged view MUST start from the current content of each session's buffer and then keep receiving new lines, inserting them at their chronological position.
- **FR-008**: The merged view MUST support Follow (stick to the newest line), turning off when the user scrolls up, as in a normal tab.
- **FR-009**: The merged view MUST support the level filters and the text/regex filter (with context), and let users show/hide each session.
- **FR-010**: Users MUST be able to jump from a merged row to the same line in its source tab.
- **FR-011**: Users MUST be able to save the visible rows of the merged view to a text file, including the session name on each row.
- **FR-012**: The merged view MUST NOT alter source sessions, their buffers or their saved configuration, and MUST NOT trigger alerts/badges of its own.
- **FR-013**: All new user-visible text MUST be available in Italian and English, and the user guides, README and changelog MUST describe the feature.

### Key Entities

- **Merged view**: a read-only chronological combination of the lines of several sessions, with its own filters and Follow state.
- **Merged row**: one line plus its source session, its effective timestamp and its position tie-break.
- **Session colour**: a colour assigned to each included session for the duration of the view.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: With two sessions of 100,000 lines each, the merged view is shown in under 3 seconds and scrolling stays smooth.
- **SC-002**: 100% of lines are shown in non-decreasing effective-timestamp order (a line without its own timestamp counts with the timestamp of the previous line of its session) in the unfiltered view, on a test log set with interleaved, tied, timestamp-less and out-of-order lines.
- **SC-003**: A new line arriving in any included session appears in its correct place within 1 second.
- **SC-004**: A user can tell which session produced any row without reading the line text (session column colour and name always visible).
- **SC-005**: A user can go from "I see a failure in service A" to "what service B logged at the same moment" in a single view, without scrolling two separate panes.

## Assumptions

- The merge works on the lines already held by each open tab (like cross-tab search): it does not read files by itself, so sessions that are not open cannot be included.
- Sorting uses the same timestamp recognition as the existing search and go-to-time features; no new timestamp formats are added.
- The merged view is a view only: it is not saved in the workspace and is not exported with sessions.
- At most one merged view is open at a time; asking for another one selects the existing one (new sessions opened since are not added: close and reopen it).
- "Merged view" is the feature name; in the interface it is a tab called "Unione" (en: "Merged"), opened by the *Unisci…* command (Ctrl+Shift+M).
- Per-session colours are picked automatically from a fixed palette; user-defined colours are out of scope.
- Time zones are not normalised: timestamps are compared as written.
- Merging beyond the per-session buffer limit (older lines already trimmed) is out of scope.
