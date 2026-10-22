# Quickstart / validation

Prereqs: `dotnet build`; kill a running `RemoteLogViewer.exe` first (`taskkill //IM RemoteLogViewer.exe //F`).

## Automated

```powershell
dotnet test --filter "FullyQualifiedName~ErrorHistogramTests"
dotnet test --filter "FullyQualifiedName~AiTests"
dotnet test
```

Expected: window zero-fill, timestamp inheritance, alert counting, trim/reset, `Excerpt` edges (start/end of buffer), explain preset present in both languages, guide section ids equal.

## Manual (local file, no SMB)

1. Create a log with `yyyy-MM-dd HH:mm:ss ERROR ...` lines: 2 errors at 14:31, 9 at 14:32, 1 at 14:35, INFO lines between. *Apri file…* it.
2. Histogram: bars at 14:31/14:32/14:35, 14:32 tallest, 14:33–14:34 empty slots; tooltip shows minute and count (spec US1-1, 3).
3. Click the 14:32 bar: first 14:32 line selected, Follow off (US1-2). Hide ERROR via checkbox, click again: nearest visible line at/after (US1-5).
4. Append an ERROR line to the file: bar updates within ~1 s (US1-4). *Pulisci*: strip empties (US1-6).
5. Right-click a middle line → *Spiega questa riga*: AI window opens with only that tab checked, explain preset in the prompt; start (a harmless command such as `findstr "^"`) and verify the echoed input holds ≤ 41 lines with the target marked `>>> ` (US2-1, 2). Pick a line near the file start: fewer lines.
6. Reopen the command with the AI window already open: same window re-filled (US2-3). Switch language: menu entry and preset follow (US2-6).
7. Clean up `sessions.json` / `ai.json` if the manual run changed them.
