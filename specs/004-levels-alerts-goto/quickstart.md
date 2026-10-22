# Quickstart: Custom levels, custom alerts and go to time

## Automated

```powershell
dotnet test --filter "FullyQualifiedName~LogLevelTests|FullyQualifiedName~LogTimestampTests|FullyQualifiedName~SessionConfigTests|FullyQualifiedName~GuideTests|FullyQualifiedName~LocTests"
dotnet test
```

## Manual

Prepare `C:\temp\rlv.log`:

```text
10:00:00 [I] start
14:30:00 [E] boom
   at Foo.Bar()
14:35:00 [I] request timeout
```

1. **Custom levels**: create a session on `\\localhost\C$`, file `temp\rlv.log`; in *Livelli e avvisi* set ERROR `\[E\]`, INFO `\[I\]`. Open it: ERROR 1, INFO 2; unchecking ERROR hides `boom` and its stack line.
2. **Invalid pattern**: set ERROR to `[` → Save refused with a message naming ERROR.
3. **Custom alert**: set alert `timeout`; open the session, select another tab, append `14:40:00 [I] another timeout` to the file → badge on the session tab; with the window inactive the taskbar flashes.
4. **Go to time**: in the tab press `Ctrl+G`, type `14:32` → the `14:35:00` line is selected, Follow off. Type `23:00` → "no line" message. Type `abc` → error, dialog stays open.
5. Switch language to English: new labels/messages are English.

Clean up the test session from `sessions.json` afterwards.
