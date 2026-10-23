# Quickstart: Line bookmarks

1. `dotnet test --filter "FullyQualifiedName~BookmarksTests"` — navigation helper (next/prev, wrap, skip, none).
2. `dotnet run --project src/RemoteLogViewer`, open a local log (*Apri file…*).
3. Select 3 lines, F2 on each: marker glyph + tint; footer shows "3 segnalibri". F2 again on one: removed.
4. Empty search box: F3 cycles forward and wraps, Shift+F3 backwards.
5. Type text in the search box: F3 goes to search matches only.
6. Untick ERROR (hides a bookmarked error line): F3 skips it; re-tick: bookmark still there.
7. *▲ Precedenti*: bookmarks stay, view does not jump. *Pulisci*: bookmarks gone, F3 shows "Nessun segnalibro".
