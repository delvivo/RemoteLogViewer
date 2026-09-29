# Remote Log Viewer

App desktop Windows (WPF, .NET 10) per seguire log su share remote. Sostituisce:

```powershell
New-SmbMapping -RemotePath "\\server\E$" -UserName "user" -Password "pwd"
Get-Content -Tail 1000 -Wait "\\server\E$\logs\app.log"
```

## Funzioni

- Sessioni salvate (share, utente, password, file, righe iniziali, encoding) in `%APPDATA%\RemoteLogViewer\sessions.json`; password cifrate con DPAPI (utente Windows corrente).
- Tail in tempo reale (polling 500 ms), rilevamento troncamento/rotazione, attesa file mancante, riconnessione automatica.
- Più sessioni in schede o **Affianca** (tutte visibili insieme).
- Colori per livello (ERROR/WARN/INFO/DEBUG), filtro testo/regex, ricerca con evidenziazione (Ctrl+F, F3/Shift+F3), Follow automatico (si ferma scrollando in su), Ctrl+C copia righe, buffer max 100.000 righe.

## Build / avvio

```powershell
dotnet build
dotnet test
dotnet run --project src/RemoteLogViewer
```

`nuget.config` locale punta a nuget.org (la config NuGet globale può avere feed privati).

## Note

- Connessione share senza lettera di unità (`WNetAddConnection2`); rilasciata alla chiusura della sessione/app. Mappature già esistenti create fuori dall'app non vengono toccate.
- Errore "credenziali diverse" (1219): Windows ammette una sola identità per server; chiudi la connessione esistente (`net use \\server\share /delete`) o usa le stesse credenziali.
- Specifiche: `specs/001-remote-log-viewer/`.
