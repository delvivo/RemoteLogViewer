# Research: Line bookmarks

- **Where to keep the flag** — Decision: property on `LogLine`. Rationale: identity is already guaranteed (class, not record); survives Rebuild/prepend/filter for free; trim removes it with the line. Alternatives: `HashSet<LogLine>` in `LogView` (needs cleanup on trim/clear, leaks), index list (breaks on shifts).
- **F3 conflict** — Decision: search wins when the search box has text, else bookmarks (spec clarification). Alternatives: new keys for search, or other keys for bookmarks (rejected by user).
- **Empty-state message** — Decision: `Info()` message box, consistent with *Vai all'ora*. Alternative: transient status text (more code, no existing mechanism).
- **Marker visual** — Decision: glyph column + background tint, not colour alone. Alternative: only background (rejected, colour-only).
