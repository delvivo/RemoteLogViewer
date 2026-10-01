# Remote Log Viewer

[English](README.md) · **Italiano**

App desktop Windows (WPF, .NET 10) per seguire log su share remote. Sostituisce:

```powershell
New-SmbMapping -RemotePath "\\server\E$" -UserName "user" -Password "pwd"
Get-Content -Tail 1000 -Wait "\\server\E$\logs\app.log"
```

## Funzioni

- Icona dell'applicazione (`src/RemoteLogViewer/app.ico`): exe e finestra principale.
- Sessioni salvate (share, utente, password, file, righe iniziali, encoding) in `%APPDATA%\RemoteLogViewer\sessions.json`; password cifrate con DPAPI (utente Windows corrente).
- **Credenziali** condivise (pulsante *Credenziali…* nella barra): nome + utente + password, selezionabili nel campo *Credenziale* di ogni sessione in alternativa a utente/password propri. Al cambio password si aggiorna solo la credenziale; le sessioni già aperte la usano dalla prossima apertura. Eliminare una credenziale riporta le sessioni che la usavano a utente/password propri. Le credenziali non vengono esportate: l'import ricollega la credenziale solo se esiste già (stesso PC).
- Tail in tempo reale (polling 500 ms), rilevamento troncamento/rotazione, attesa file mancante, riconnessione automatica.
- **Collezioni** (cartelle annidabili) per organizzare le sessioni: pulsante *Collezione*, tasto destro per nuova/rinomina/duplica/sposta/elimina, trascinamento di sessioni e collezioni (su spazio vuoto = radice). F2 = modifica/rinomina, Canc = elimina. *Duplica* copia la collezione con tutto il contenuto (password incluse) accanto all'originale come `Nome (copia)`.
- **Esporta / importa** una collezione (o tutto, tasto destro su spazio vuoto) in un file `.rlv.json`. Le password **non** vengono esportate: dopo l'import vanno reinserite con *Modifica*. L'import aggiunge sempre copie nuove (nomi in conflitto → `Nome (2)`), non sovrascrive nulla.
- **Data nel percorso**: `{date:<formato>}` nel campo *File*, in una cartella o nel nome, es. `DOB\DOB_SDL\log\standard_logs\{date:yyyy_MM_dd}\log.log` o `logs\app_{date:yyyy-MM-dd}.log`. All'avvio si sceglie la data su un calendario (default oggi, con anteprima del percorso): i giorni del mese visualizzato senza file di log sono barrati e non selezionabili (oggi resta selezionabile: la sessione attende il file). Se la data scelta è oggi, a mezzanotte la sessione passa da sola al file del nuovo giorno (riga `— nuovo giorno —`); una data passata resta fissa. Solo componenti di data (niente ore/minuti), non ammesso nella share. Se solo i log vecchi hanno la data e quello corrente no (es. oggi `app.log`, ieri `app.2026-09-29.log`), scrivi `logs\app{date:'.'yyyy-MM-dd}.log` (separatore tra apici dentro il formato) e spunta *Il file di oggi non ha la data*: per oggi il placeholder sparisce e a mezzanotte la sessione resta sullo stesso file (la rotazione lato server viene rilevata come sempre).
- Più sessioni in schede (riordinabili trascinandole) o **Affianca** (tutte visibili insieme, nello stesso ordine delle schede).
- Colori per livello (ERROR/WARN/INFO/DEBUG), filtro per livello (checkbox ERROR/WARN/INFO/DEBUG; le righe senza livello restano sempre visibili), filtro testo/regex, ricerca con evidenziazione (Ctrl+F, F3/Shift+F3), Follow automatico (si ferma scrollando in su), pulsante *A capo* per mandare a capo le righe lunghe invece dello scroll orizzontale, Ctrl+C copia righe, buffer max 100.000 righe.
- **Contatori per livello** nelle checkbox (`ERROR 12`): voci nel buffer (uno stack trace conta 1), indipendenti dai filtri, azzerati da *Pulisci*.
- **Contesto** (0–50) accanto al filtro: mostra N righe prima/dopo ogni riga filtrata, come `grep -C N`; righe di contesto attenuate, gruppi separati da `--`.
- **▲ Precedenti**: carica sopra le righe già lette un altro blocco (= *Righe iniziali*, 1000 se 0) senza interrompere il tail; disabilitato a inizio file, dopo una rotazione o con sessione non attiva.
- **Cerca…** / Ctrl+Maiusc+F: ricerca avanzata tra *tutte* le sessioni aperte in una finestra non modale: testo o regex, maiuscole/minuscole, parola intera, filtro per livello, intervallo data+ora; risultati raggruppati per scheda (max 10.000, istantanea dei buffer); il doppio click salta alla riga nella sua scheda (azzerando i filtri della scheda se la nascondono); *Esporta…* salva i risultati in un file.
- **Salva…** / Ctrl+S: salva le righe visibili (filtri e contesto applicati) in un `.log` UTF-8.
- **Avvisi**: badge rosso con il numero di ERROR sulle schede non visibili (azzerato selezionandole; niente badge in Affianca) e lampeggio della taskbar se l'app non è in primo piano. Le righe lette all'apertura non avvisano.
- **Riapertura all'avvio**: alla chiusura si memorizzano schede aperte, ordine, selezione e Affianca; all'avvio vengono riaperte senza domande (sessioni con data su "oggi" → data corrente, data fissa → stessa data).
- **Guida utente** (F1 o pulsante *Guida*): pagina HTML in italiano o inglese, aperta nel browser predefinito, funziona offline.
- **Lingua** (it/en): selettore *Lingua* nella barra; vale per interfaccia e guida, il cambio è immediato, senza riavvio. Default: lingua di Windows (italiano, altrimenti inglese). Salvata in `%APPDATA%\RemoteLogViewer\lang.txt`.

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
- Specifiche: `specs/001-remote-log-viewer/`, `specs/001-remote-log-viewer-collections-tabs-datepath/`, `specs/002-usability-improvements/`.
- Guida utente: `src/RemoteLogViewer/Guida.it.html` / `Guida.en.html` (risorse incorporate nell'exe).
