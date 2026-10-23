# Implementation Plan: Line bookmarks

**Branch**: `feature/007-line-bookmarks` | **Date**: 2026-10-23 | **Spec**: [spec.md](spec.md)

## Summary

A bookmark is a flag on the `LogLine` object itself (`IsBookmarked`, with `INotifyPropertyChanged` like `IsMatch`), so it survives `Rebuild`, prepend and filter changes by identity, and disappears with the line on trim/*Pulisci*. `LogView` gets a `ToggleBookmark` command (F2) and extends the existing F3 / Shift+F3 `Find`: with an empty search box it jumps to the next/previous visible bookmark. Navigation logic is a small pure static helper (tested).

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0-windows`), WPF. **Dependencies**: none new. **Storage**: none (in memory only). **Testing**: xUnit for the pure helper; UI via quickstart. **Performance**: next-bookmark = linear scan of `_visible` (≤ 100k) per key press, well under 200 ms.

**Constraints**: `LogLine` stays a class; UI thread only; `ContextFilter` context lines keep their own `IsContext` and are not affected.

## Constitution Check

Constitution is the unfilled template: no gates. `CLAUDE.md` rules apply (L.En for new strings, `Checked` guards, guides/README/CHANGELOG/CLAUDE.md updated). No violations.

## Project Structure

```text
specs/007-line-bookmarks/  plan.md research.md data-model.md quickstart.md tasks.md
src/RemoteLogViewer/
├── LogLevel.cs        # LogLine.IsBookmarked (INPC)
├── Bookmarks.cs       # NEW pure: Next(IReadOnlyList<LogLine>, selectedIndex, dir) -> index or -1
├── LogView.xaml(.cs)  # F2 binding + ToggleBookmark command, F3 dispatch, row marker, count in CountLabel
├── L.cs               # en strings
├── Guida.it.html / Guida.en.html
tests/RemoteLogViewer.Tests/BookmarksTests.cs  # NEW
README.md, README.it.md, CHANGELOG.md, CLAUDE.md
```

## Design notes

- **State.** `LogLine.IsBookmarked` raises `PropertyChanged`; the item style gets a `DataTrigger` on it. No separate set: nothing to keep in sync on trim (`_all.RemoveRange`), `Clear_Click`, `Older` prepend or `Rebuild`.
- **Marker.** The item `DataTemplate` becomes a `DockPanel`: a narrow `●` glyph column visible only when bookmarked, plus a light blue row tint (`#BBDEFB`) via `DataTrigger`, declared before `IsMatch` so a search hit (yellow) still wins. The glyph keeps the meaning when colour is not enough (accessibility).
- **Toggle (F2).** `LogView.ToggleBookmark` `RoutedCommand`, `KeyBinding F2` in `UserControl.InputBindings` (fires only when focus is inside the log view; the tree's F2 = Edit is on its own `KeyDown`, no clash). Acts on `Lines.SelectedItem as LogLine`; ignored if null or `IsMarker`. Context-separator rows are not `LogLine`s in `_visible`. Also a row context-menu item *Segnalibro (F2)*. Calls `UpdateCount()`.
- **Navigate (F3 / Shift+F3).** Existing `Find(dir)` returns early when `_search` is empty; that branch now calls `GoToBookmark(dir)`. `Bookmarks.Next(_visible, Lines.SelectedIndex, dir)` scans with wrap, skipping non-bookmarked rows (hidden ones are not in `_visible`, FR-007); none → `Info(...)` message "Nessun segnalibro". Selection + `ScrollIntoView` + Follow off, as `Find`. Resolves spec clarification (search wins when box has text).
- **Count (FR-008).** `UpdateCount` appends ` · N segnalibri` to `CountLabel` when N > 0 (O(n) like the existing `IsMatch` count).
- **Out of scope.** Merged view, search results, persistence, bookmark panel.
- **i18n/docs.** Italian keys in code/XAML, English in `L.En`; shortcut table rows F2 / F3 in both guides, README(s), CHANGELOG `Added`, CLAUDE.md LogView paragraph.

## Complexity Tracking

No violations.
