# Feature Specification: Line bookmarks

**Feature Branch**: `feature/007-line-bookmarks`

**Created**: 2026-10-23

**Status**: Draft

**Input**: User description: "Bookmarks on lines. F2 sets or clears the bookmark, F3 / Shift+F3 jump to the next or previous one. LogLine is a class with its own identity, so bookmarks survive Rebuild and trim."

## Clarifications

### Session 2026-10-23

- Q: F3 / Shift+F3 are already search next/previous; what do they do when both search text and bookmarks exist? → A: Search wins while the search box has text; with an empty search box they jump between bookmarks.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Mark and clear a line (Priority: P1)

While reading a log, the user finds an interesting line and wants to find it again later. They select the line and press F2: the line shows a bookmark marker. Pressing F2 again on the same line removes it.

**Why this priority**: without marking there is nothing to navigate.

**Independent Test**: select a line, press F2, the marker appears; press F2 again, it disappears.

**Acceptance Scenarios**:

1. **Given** a selected unmarked line, **When** the user presses F2, **Then** the line shows a bookmark marker.
2. **Given** a selected bookmarked line, **When** the user presses F2, **Then** the marker is removed.
3. **Given** no selected line (or a context-separator/marker row), **When** the user presses F2, **Then** nothing changes.
4. **Given** a bookmarked line, **When** the level or text filter changes, the buffer is rebuilt, or older lines are loaded, **Then** the same line is still bookmarked when it is visible.

---

### User Story 2 - Jump between bookmarks (Priority: P1)

The user presses F3 to go to the next bookmark and Shift+F3 to go to the previous one, in the order of the log. The target line is selected and scrolled into view. Navigation wraps around at the ends.

**Why this priority**: it is the reason to bookmark: jump between points of interest in a long log.

**Independent Test**: bookmark three lines, press F3 repeatedly: selection goes 1 → 2 → 3 → 1; Shift+F3 goes backwards.

**Acceptance Scenarios**:

1. **Given** an empty search box, bookmarks on lines 10, 50, 90 and line 20 selected, **When** the user presses F3, **Then** line 50 is selected and visible.
2. **Given** line 90 selected, **When** the user presses F3, **Then** line 10 is selected (wrap).
3. **Given** line 10 selected, **When** the user presses Shift+F3, **Then** line 90 is selected (wrap).
4. **Given** no bookmarks in the tab, **When** the user presses F3 or Shift+F3, **Then** nothing changes and the status bar says there are no bookmarks.
5. **Given** text in the search box, **When** the user presses F3, **Then** the selection moves to the next search match, not to a bookmark.
6. **Given** a bookmarked line hidden by the current filters, **When** the user navigates, **Then** hidden bookmarks are skipped (only visible bookmarks are visited).

---

### User Story 3 - Bookmarks survive buffer changes (Priority: P2)

Bookmarks stay on the same line when the view is rebuilt (filter change, load of previous lines) and are dropped only when the line itself leaves the buffer (trimmed past the buffer limit, or cleared with *Pulisci*).

**Why this priority**: losing bookmarks on a filter change would make the feature unreliable.

**Independent Test**: bookmark a line, toggle a level filter off and on, load previous lines: the bookmark is still there. Fill the buffer past the limit so the line is trimmed: the bookmark is gone and navigation does not fail.

**Acceptance Scenarios**:

1. **Given** a bookmarked line, **When** a level filter hides it and later shows it again, **Then** it is still bookmarked.
2. **Given** a bookmarked line, **When** older lines are prepended, **Then** it is still bookmarked and the view does not jump.
3. **Given** a bookmarked line trimmed from the buffer, **When** the user navigates, **Then** it is no longer visited and no error occurs.

---

### Edge Cases

- Two identical log lines: bookmarking one must not bookmark the other.
- Bookmark count shown in the tab's status so the user knows how many exist.
- Bookmarks in a session tab are independent of other tabs.
- The merged timeline tab and cross-tab search results are out of scope (see Assumptions).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Users MUST be able to toggle a bookmark on the selected log line with F2.
- **FR-002**: A bookmarked line MUST display a visible marker.
- **FR-003**: When the search box is empty, F3 MUST select and reveal the next visible bookmark after the selection; Shift+F3 the previous one; both MUST wrap around. When the search box has text, F3 / Shift+F3 keep their existing meaning (next/previous search match) and do not touch bookmarks.
- **FR-004**: Bookmarks MUST belong to the individual line, not to its position, so they survive filtering, context changes, prepending older lines and buffer trimming of other lines.
- **FR-005**: A bookmark MUST be discarded when its line leaves the buffer (trim or clear).
- **FR-006**: With no bookmarks, F3 / Shift+F3 MUST do nothing visible except a status message.
- **FR-007**: Navigation MUST visit only lines currently visible under the active filters.
- **FR-008**: The tab MUST show how many bookmarks it has.
- **FR-009**: The shortcuts MUST be listed in the user guides and README, and all new UI strings MUST be localized (it/en).

### Key Entities

- **Bookmark**: a flag on a single log line of a session tab; identity is the line itself.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can mark a line and return to it from anywhere in a 100,000-line log with at most 2 key presses per jump.
- **SC-002**: 100% of bookmarks on lines still in the buffer remain after any filter change or load of previous lines.
- **SC-003**: Jumping to the next bookmark in a 100,000-line buffer feels instant (under 200 ms).

## Assumptions

- Bookmarks are in memory only: not saved in the workspace, not exported, lost when the tab is closed.
- Bookmarks apply to session/local-file log tabs only; the merged timeline and search results do not get bookmarks in this version.
- F2/F3 act on the focused log view of the selected tab (in Affianca, the view that has the focus).
- Pulisci removes all bookmarks of that tab (the lines are gone).
- No bookmark list/panel and no bookmark notes in this version.
