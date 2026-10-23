# Data Model: Line bookmarks

**LogLine** (existing class) gains `IsBookmarked: bool` (default false, raises `PropertyChanged`). Not persisted, not exported, not copied by `MergedLine`.

Lifecycle: set/cleared by F2 on a line in a `LogView`; ends when the line leaves `_all` (trim, *Pulisci*) or the tab closes.
