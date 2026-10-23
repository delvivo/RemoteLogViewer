# Quickstart / validation

Prereqs: `dotnet build`; kill a running `RemoteLogViewer.exe` first (`taskkill //IM RemoteLogViewer.exe //F`).

## Automated

```powershell
dotnet test --filter "FullyQualifiedName~TimelineMergeTests"
dotnet test
```

Expected: interleaving by timestamp, continuation lines glued to their head line, equal timestamps ordered by session then arrival, late line inserted in place (`Add` returns `false`), prepended block placed before, timestamp-less session sorted first with `HasTimestamps == false`, markers inherit, leading no-timestamp rows re-timed after a prepend, trim at the cap, 2×100,000 interleaved lines merged in under 3 s, guide section ids equal in both languages.

## Manual (two local files, no SMB)

1. Create `a.log` and `b.log` with `yyyy-MM-dd HH:mm:ss LEVEL msg` lines: A at 10:00:01 and 10:00:03 (the 10:00:03 one followed by a stack-trace line), B at 10:00:02. *Apri file…* both.
2. Toolbar *Unisci…* (or `Ctrl+Shift+M`): a new tab "Unione (2)" opens with rows A, B, A (+ the stack-trace line right after A's 10:00:03), session column in two colours (US1-1, 2).
3. Uncheck session B: only A rows (US3-1). Re-check: order restored. Set a level filter and a text filter: applied across sessions (US3-2).
4. Append a B line at 10:00:02 (late) and an A line at 10:00:04: they show in their chronological places, view stays at the bottom with Follow on (US2-1, 2).
5. Double-click a row: the source tab is selected and the line revealed (US3-3); with *Affianca* on, the line is revealed without changing tabs.
6. Close tab B: its rows stay, its checkbox shows it as closed, double-click on its rows does nothing/explains (US2-3). *Pulisci* in A: merged rows stay, new A lines still arrive (US2-4).
6b. With the merged tab selected (not the source tab), append an ERROR line to `a.log` while the window is in the background: the taskbar does not flash and no badge appears on the merged tab (FR-012).
7. `Ctrl+S` in the merged tab: file lines are `[session] text`. Open *Cerca…* and *Analisi IA…*: the merged tab is not listed as a source.
8. Close the app with the merged tab open and restart: only the two session tabs come back, selection stays on a valid tab.
9. With only one session open, *Unisci…* shows the "at least two sessions" message.
10. Switch language: labels of the merged tab follow. Clean up `sessions.json` if the manual run changed it.
