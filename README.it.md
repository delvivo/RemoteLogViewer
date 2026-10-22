# Remote Log Viewer

[English](README.md) · **Italiano**

App desktop Windows (WPF, .NET 10) per seguire log su share remote. È pensata per i server Windows. Sostituisce:

```powershell
New-SmbMapping -RemotePath "\\server\E$" -UserName "user" -Password "pwd"
Get-Content -Tail 1000 -Wait "\\server\E$\logs\app.log"
```

![Tail in tempo reale: le nuove righe scorrono, un badge rosso conta gli errori arrivati su una scheda nascosta](docs/images/live-tail.gif)

## Screenshot

*Sessioni, server e righe di log qui sotto sono dati demo inventati. Le immagini mostrano l'interfaccia in inglese.*

| | |
|---|---|
| ![Finestra principale: albero sessioni con collezioni, schede, livelli colorati e contatori per livello](docs/images/main.png) | ![Filtro regex con 2 righe di contesto, gruppi separati da --](docs/images/filter.png) |
| Sessioni in collezioni, schede, colori e contatori per livello | Filtro testo/regex con contesto stile `grep -C` |
| ![Affianca: tre sessioni visibili insieme](docs/images/side-by-side.png) | ![Ricerca avanzata su tutte le sessioni aperte, solo ERROR e WARN](docs/images/search.png) |
| *Affianca* | *Cerca…* su tutte le sessioni aperte |

## Funzioni

- Icona dell'applicazione (`src/RemoteLogViewer/app.ico`): exe e finestra principale.
- Sessioni salvate (share, utente, password, file, righe iniziali, encoding) in `%APPDATA%\RemoteLogViewer\sessions.json`; password cifrate con DPAPI (utente Windows corrente).
- **Credenziali** condivise (pulsante *Credenziali…* nella barra): nome + utente + password, selezionabili nel campo *Credenziale* di ogni sessione in alternativa a utente/password propri. Al cambio password si aggiorna solo la credenziale; le sessioni già aperte la usano dalla prossima apertura. Eliminare una credenziale riporta le sessioni che la usavano a utente/password propri. Le credenziali non vengono esportate: l'import ricollega la credenziale solo se esiste già (stesso PC).
- Tail in tempo reale (polling 500 ms), rilevamento troncamento/rotazione, attesa file mancante, riconnessione automatica.
- **Collezioni** (cartelle annidabili) per organizzare le sessioni: pulsante *Collezione*, tasto destro per nuova/rinomina/duplica/sposta/elimina, trascinamento di sessioni e collezioni (su spazio vuoto = radice). F2 = modifica/rinomina, Canc = elimina. *Duplica* copia la collezione con tutto il contenuto (password incluse) accanto all'originale come `Nome (copia)`.
- **Esporta / importa** una collezione (o tutto, tasto destro su spazio vuoto) in un file `.rlv.json`. Le password **non** vengono esportate: dopo l'import vanno reinserite con *Modifica*. L'import aggiunge sempre copie nuove (nomi in conflitto → `Nome (2)`), non sovrascrive nulla.
- **Data nel percorso**: `{date:<formato>}` nel campo *File*, in una cartella o nel nome, es. `FOLDER\APP_NAME\log\standard_logs\{date:yyyy_MM_dd}\log.log` o `logs\app_{date:yyyy-MM-dd}.log`. All'avvio si sceglie la data su un calendario (default oggi, con anteprima del percorso): i giorni del mese visualizzato senza file di log sono barrati e non selezionabili (oggi resta selezionabile: la sessione attende il file). Se la data scelta è oggi, a mezzanotte la sessione passa da sola al file del nuovo giorno (riga `— nuovo giorno —`); una data passata resta fissa. Solo componenti di data (niente ore/minuti), non ammesso nella share. Se solo i log vecchi hanno la data e quello corrente no (es. oggi `app.log`, ieri `app.2026-09-29.log`), scrivi `logs\app{date:'.'yyyy-MM-dd}.log` (separatore tra apici dentro il formato) e spunta *Il file di oggi non ha la data*: per oggi il placeholder sparisce e a mezzanotte la sessione resta sullo stesso file (la rotazione lato server viene rilevata come sempre).
- Più sessioni in schede (riordinabili trascinandole) o **Affianca** (tutte visibili insieme, nello stesso ordine delle schede).
- Colori per livello (ERROR/WARN/INFO/DEBUG), filtro per livello (checkbox ERROR/WARN/INFO/DEBUG; le righe senza livello restano sempre visibili), filtro testo/regex, ricerca con evidenziazione (Ctrl+F, F3/Shift+F3), Follow automatico (si ferma scrollando in su), pulsante *A capo* per mandare a capo le righe lunghe invece dello scroll orizzontale, Ctrl+C copia righe, buffer max 100.000 righe.
- **Livelli e avvisi personalizzati** (finestra della sessione, *Livelli e avvisi*): regex facoltative, senza distinzione maiuscole/minuscole, per ERROR/WARN/INFO/DEBUG, per i log con marcatori di livello non standard (`[E]`, `|ERR|`, `level=50`): confrontate in ordine di gravità, poi vale il riconoscimento standard; filtri, contatori, badge e ricerca si adeguano. Una regex *Avviso* (es. `OutOfMemory|deadlock|timeout`) fa avvisare come gli errori le nuove righe che corrispondono, di qualsiasi livello. Valgono dalla prossima apertura della sessione.
- **Contatori per livello** nelle checkbox (`ERROR 12`): voci nel buffer (uno stack trace conta 1), indipendenti dai filtri, azzerati da *Pulisci*.
- **Contesto** (0–50) accanto al filtro: mostra N righe prima/dopo ogni riga filtrata, come `grep -C N`; righe di contesto attenuate, gruppi separati da `--`.
- **▲ Precedenti**: carica sopra le righe già lette un altro blocco (= *Righe iniziali*, 1000 se 0) senza interrompere il tail; disabilitato a inizio file, dopo una rotazione o con sessione non attiva. **⏫ Tutto** carica in un colpo tutto fino all'inizio del file (entro le 100.000 righe del buffer).
- **Cerca…** / Ctrl+Maiusc+F: ricerca avanzata tra *tutte* le sessioni aperte in una finestra non modale: testo o regex, maiuscole/minuscole, parola intera, filtro per livello, intervallo data+ora; risultati raggruppati per scheda (max 10.000, istantanea dei buffer); il doppio click salta alla riga nella sua scheda (azzerando i filtri della scheda se la nascondono); *Esporta…* salva i risultati in un file.
- **Analisi IA…** / Ctrl+Maiusc+I: invia le righe visibili delle schede spuntate (max 1 MB, si tengono le più recenti) con un prompt (*Report errori*, *Riepilogo* o una domanda libera) a uno strumento IA a riga di comando già installato e autenticato: Claude Code (`claude -p --tools "" --no-session-persistence`), Codex (`codex exec --skip-git-repo-check --sandbox read-only --ephemeral -`), OpenCode (`opencode run --agent plan --print-logs --log-level WARN`), Antigravity CLI (`agy --add-dir . -p="Read the file input.md in the current directory and follow the instructions it contains."`: `agy` ignora lo stdin di testo, quindi l'input viene scritto anche in `input.md` nella cartella di lavoro temporanea) o un comando personalizzato (eseguito con `cmd`, prompt e log su stdin). La risposta compare man mano nella finestra e si può annullare, copiare, aprire formattata nel browser o esportare in `.md`/`.html`. Conferma al primo uso di ogni strumento; impostazioni in `%APPDATA%\RemoteLogViewer\ai.json`.
- **Apri file…** / Ctrl+O (o trascinamento da Esplora risorse): apre un log locale (`.log`, `.txt`, qualunque file di testo) in una nuova scheda, senza sessione, credenziali né SMB: ultime 1000 righe poi Follow, stessi filtri/ricerca/ricerca avanzata/avvisi delle sessioni remote. Non entra nell'elenco sessioni; viene ricordato e riaperto all'avvio se il file esiste ancora.
- **Archivi .gz**: un percorso che finisce con `.gz` (log ruotato e compresso dal server), locale o da sessione, viene decompresso una volta e mostrato in sola lettura: niente tail/Follow, Avvia/Stop disattivato, ultime 100.000 righe, nessun avviso. Con *Il file di oggi non ha la data*, `logspp{date:'.'yyyy-MM-dd}.log.gz` apre `app.log` per oggi e l'archivio per le altre date. Parti numerate dello stesso giorno (`hr-portal.log.2026-10-03.0.gz`, `.1.gz`, …): scrivi `{n}` al posto del numero, es. `logs\hr-portal.log{date:'.'yyyy-MM-dd}.{n}.gz`; tutte le parti vengono lette in ordine di numero, e per oggi `{n}` (con il suo punto) e `.gz` spariscono (`hr-portal.log`).
- **Salva…** / Ctrl+S: salva le righe visibili (filtri e contesto applicati) in un `.log` UTF-8.
- **Istogramma errori**: una striscia sopra ogni scheda con una barra per minuto (voci ERROR e avvisi personalizzati); scorre in orizzontale su tutto il log e si nasconde con il pulsante *Istogramma*; il tooltip mostra il conteggio, il click su una barra porta alla prima riga visibile di quel minuto.
- **Spiega questa riga**: tasto destro su una riga → si apre l'Analisi IA con solo quella scheda, il prompt *Spiega questa riga* e la riga più 20 righe visibili di contesto per lato (riga segnata con `>>> `).
- **Vai all'ora…** / Ctrl+G: scrivi `14:32` (o `2026-10-22 14:32`, `22/10/2026 14:32`) e viene selezionata la prima riga visibile a quell'ora o dopo (Follow disattivato). Le righe senza orario prendono quello precedente; se l'ora è prima della prima riga letta viene suggerito *▲ Precedenti*.
- **Avvisi**: badge rosso con il numero di ERROR (e di righe di avviso personalizzato) sulle schede non visibili (azzerato selezionandole; niente badge in Affianca) e lampeggio della taskbar se l'app non è in primo piano. Le righe lette all'apertura non avvisano.
- **Riapertura all'avvio**: alla chiusura si memorizzano schede aperte, ordine, selezione e Affianca; all'avvio vengono riaperte senza domande (sessioni con data su "oggi" → data corrente, data fissa → stessa data).
- **Guida utente** (F1 o pulsante *Guida*): pagina HTML in italiano o inglese, aperta nel browser predefinito, funziona offline.
- **Lingua** (it/en): selettore *Lingua* nella barra; vale per interfaccia e guida, il cambio è immediato, senza riavvio. Default: lingua di Windows (italiano, altrimenti inglese). Salvata in `%APPDATA%\RemoteLogViewer\lang.txt`.

## Installazione

Ogni release GitHub (tag `v*`) ha due download:

- `RemoteLogViewer-vX.Y.Z-setup.exe`: installer per utente (`%LOCALAPPDATA%\Programs\RemoteLogViewer`, senza diritti di amministratore), con collegamento nel menu Start e disinstallazione. Eseguire il setup di una versione più recente aggiorna quella installata. Sessioni e impostazioni in `%APPDATA%\RemoteLogViewer` restano.
- `RemoteLogViewer-vX.Y.Z-win-x64.zip`: exe singolo portabile.

L'installer è generato da `.github/workflows/installer.yml` (Inno Setup, script `installer/RemoteLogViewer.iss`) dopo che il workflow di release è andato a buon fine.

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
