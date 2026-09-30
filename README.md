# Remote Log Viewer

App desktop Windows (WPF, .NET 10) per seguire log su share remote. Sostituisce:

```powershell
New-SmbMapping -RemotePath "\\server\E$" -UserName "user" -Password "pwd"
Get-Content -Tail 1000 -Wait "\\server\E$\logs\app.log"
```

## Funzioni

- Sessioni salvate (share, utente, password, file, righe iniziali, encoding) in `%APPDATA%\RemoteLogViewer\sessions.json`; password cifrate con DPAPI (utente Windows corrente).
- Tail in tempo reale (polling 500 ms), rilevamento troncamento/rotazione, attesa file mancante, riconnessione automatica.
- **Collezioni** (cartelle annidabili) per organizzare le sessioni: pulsante *Collezione*, tasto destro per nuova/rinomina/duplica/sposta/elimina, trascinamento di sessioni e collezioni (su spazio vuoto = radice). F2 = modifica/rinomina, Canc = elimina. *Duplica* copia la collezione con tutto il contenuto (password incluse) accanto all'originale come `Nome (copia)`.
- **Esporta / importa** una collezione (o tutto, tasto destro su spazio vuoto) in un file `.rlv.json`. Le password **non** vengono esportate: dopo l'import vanno reinserite con *Modifica*. L'import aggiunge sempre copie nuove (nomi in conflitto → `Nome (2)`), non sovrascrive nulla.
- **Data nel percorso**: `{date:<formato>}` nel campo *File*, in una cartella o nel nome, es. `DOB\DOB_SDL\log\standard_logs\{date:yyyy_MM_dd}\log.log` o `logs\app_{date:yyyy-MM-dd}.log`. All'avvio si sceglie la data (default oggi, con anteprima del percorso). Se la data scelta è oggi, a mezzanotte la sessione passa da sola al file del nuovo giorno (riga `— nuovo giorno —`); una data passata resta fissa. Solo componenti di data (niente ore/minuti), non ammesso nella share.
- Più sessioni in schede (riordinabili trascinandole) o **Affianca** (tutte visibili insieme, nello stesso ordine delle schede).
- Colori per livello (ERROR/WARN/INFO/DEBUG), filtro testo/regex, ricerca con evidenziazione (Ctrl+F, F3/Shift+F3), Follow automatico (si ferma scrollando in su), pulsante *A capo* per mandare a capo le righe lunghe invece dello scroll orizzontale, Ctrl+C copia righe, buffer max 100.000 righe.

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
- Specifiche: `specs/001-remote-log-viewer/`, `specs/001-remote-log-viewer-collections-tabs-datepath/`.
