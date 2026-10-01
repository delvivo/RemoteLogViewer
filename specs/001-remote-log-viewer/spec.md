# Feature Specification: Remote Log Viewer

**Feature Branch**: `feature/001-remote-log-viewer`

**Created**: 2026-09-29

**Status**: Draft

**Input**: User description: "Applicazione desktop Windows per visualizzare log remoti (es. su server Windows). Replica il flusso PowerShell attuale: New-SmbMapping -RemotePath \"\\\\serverName\\E$\" -UserName \"myusername\" -Password \"mypassword\" seguito da Get-Content -Tail 1000 -Wait \"path su server\". Requisiti: configurare più sessioni (server/share, credenziali, percorso file log) e salvarle; visualizzazione intelligente dei log in tail (follow in tempo reale, ultime N righe); più sessioni in parallelo visualizzabili contemporaneamente."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Seguire un log remoto in tempo reale (Priority: P1)

L'utente (sviluppatore / sistemista) inserisce share di rete remota (es. `\\serverName\E$`), credenziali e percorso del file di log. L'app si autentica sulla share, mostra le ultime N righe (default 1000) e poi aggiunge in tempo reale le nuove righe scritte nel file, come `Get-Content -Tail 1000 -Wait`.

**Why this priority**: È il flusso che oggi l'utente esegue a mano in PowerShell. Da sola sostituisce già lo script.

**Independent Test**: Configurare una sessione verso una share raggiungibile, avviarla, appendere righe al file remoto e verificare che compaiano nell'app.

**Acceptance Scenarios**:

1. **Given** share, credenziali e percorso validi, **When** l'utente avvia la sessione, **Then** vengono mostrate le ultime N righe del file (N configurabile, default 1000).
2. **Given** una sessione attiva, **When** sul server vengono scritte nuove righe nel file, **Then** compaiono nella vista entro 2 secondi senza azioni dell'utente.
3. **Given** credenziali errate o server irraggiungibile, **When** l'utente avvia la sessione, **Then** l'app mostra un messaggio d'errore comprensibile (es. "Accesso negato", "Server non raggiungibile", "File non trovato") senza bloccarsi.
4. **Given** una sessione attiva, **When** l'utente la ferma, **Then** la lettura si interrompe e la connessione alla share aperta dall'app viene rilasciata.

---

### User Story 2 - Salvare e riusare sessioni (Priority: P2)

L'utente salva più configurazioni di sessione con un nome (es. "PROD – API"), le ritrova alla riapertura dell'app e le avvia con un click. Può modificarle, duplicarle ed eliminarle.

**Why this priority**: Evita di ridigitare server/credenziali/percorso ogni volta; è il secondo motivo principale per cui serve un'app invece dello script.

**Independent Test**: Creare 2 sessioni, chiudere e riaprire l'app, verificare che siano presenti e avviabili senza reinserire la password.

**Acceptance Scenarios**:

1. **Given** una sessione configurata, **When** l'utente la salva con un nome, **Then** appare nell'elenco sessioni salvate e persiste dopo il riavvio dell'app.
2. **Given** una sessione salvata con password, **When** l'app viene riaperta, **Then** la sessione è avviabile senza reinserire la password, e la password non è leggibile in chiaro nei file dell'app.
3. **Given** una sessione salvata, **When** l'utente la modifica / duplica / elimina, **Then** l'elenco riflette la modifica.
4. **Given** più sessioni verso lo stesso server/share con le stesse credenziali, **When** vengono avviate, **Then** funzionano senza conflitti di connessione.

---

### User Story 3 - Più sessioni in parallelo (Priority: P2)

L'utente apre più sessioni contemporaneamente (server diversi o file diversi) e le vede in schede e/o affiancate (split verticale/orizzontale), ognuna in follow indipendente.

**Why this priority**: Richiesto esplicitamente; tipico durante incidenti per correlare log di più nodi.

**Independent Test**: Avviare 3 sessioni su file diversi, scrivere righe in ciascuno, verificare che ogni vista si aggiorni indipendentemente.

**Acceptance Scenarios**:

1. **Given** più sessioni salvate, **When** l'utente le avvia, **Then** ognuna si apre nella propria scheda con stato visibile (connessa / in errore / ferma).
2. **Given** due o più schede aperte, **When** l'utente sceglie la vista affiancata, **Then** le sessioni sono visibili contemporaneamente e continuano ad aggiornarsi.
3. **Given** una sessione in errore, **When** le altre sono attive, **Then** le altre non sono influenzate.

---

### User Story 4 - Visualizzazione intelligente (Priority: P3)

Durante il tail l'utente può: vedere le righe colorate per livello (ERROR, WARN, INFO, DEBUG), cercare testo, filtrare mostrando solo le righe che corrispondono, evidenziare parole chiave, mettere in pausa lo scroll automatico per leggere senza perdere le nuove righe, e riprendere il follow.

**Why this priority**: Aggiunge valore rispetto a PowerShell ma il tail base è già utile senza.

**Independent Test**: Aprire un log con livelli misti, applicare filtro "ERROR", verificare che restino solo quelle righe mentre il follow continua.

**Acceptance Scenarios**:

1. **Given** righe con livelli diversi, **When** vengono mostrate, **Then** ERROR/WARN/INFO/DEBUG hanno colori distinti.
2. **Given** una sessione attiva, **When** l'utente scrolla verso l'alto, **Then** lo scroll automatico si sospende; **When** torna in fondo o preme "Follow", **Then** riprende.
3. **Given** un filtro testuale (anche espressione regolare) attivo, **When** arrivano nuove righe, **Then** vengono mostrate solo quelle corrispondenti; rimuovendo il filtro ricompaiono tutte.
4. **Given** un testo di ricerca, **When** l'utente cerca, **Then** le occorrenze sono evidenziate e navigabili (successiva/precedente).
5. **Given** una sessione, **When** l'utente preme "Pulisci", **Then** la vista si svuota e continuano ad arrivare solo le nuove righe.

---

### Edge Cases

- File di log ruotato o troncato sul server (nuovo file con stesso nome, dimensione ridotta): l'app lo rileva, lo segnala nella vista e riprende a leggere dall'inizio del nuovo file.
- File non ancora esistente: la sessione resta in attesa e inizia a leggere quando il file compare.
- Connessione di rete persa durante il tail: stato "disconnesso", tentativi di riconnessione automatici, ripresa dal punto in cui si era arrivati senza duplicare righe.
- Share già mappata in Windows con credenziali diverse per lo stesso server: l'errore di Windows viene mostrato in modo chiaro con suggerimento.
- File molto grandi (GB): l'avvio legge solo la coda, senza scaricare l'intero file.
- Righe molto lunghe o encoding non UTF-8 (es. UTF-16, ANSI): mostrate senza caratteri corrotti; encoding selezionabile per sessione.
- Volume elevato di righe/secondo: l'interfaccia resta reattiva; oltre il limite di righe in memoria le più vecchie vengono scartate.
- Chiusura dell'app con sessioni attive: tutte le connessioni aperte dall'app vengono rilasciate.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Gli utenti MUST poter definire una sessione con: nome, percorso share remota (UNC, es. `\\server\E$`), nome utente (anche `DOMINIO\utente`), password, percorso del file di log (relativo alla share o completo UNC), numero di righe iniziali (default 1000), encoding (default automatico).
- **FR-002**: Il sistema MUST autenticarsi alla share remota con le credenziali della sessione prima di leggere il file, in modo equivalente a `New-SmbMapping`.
- **FR-003**: Il sistema MUST mostrare le ultime N righe del file all'avvio della sessione, leggendo solo la parte finale del file.
- **FR-004**: Il sistema MUST aggiungere le nuove righe scritte nel file entro 2 secondi dalla scrittura.
- **FR-005**: Il sistema MUST persistere le sessioni configurate tra un avvio e l'altro dell'app.
- **FR-006**: Il sistema MUST memorizzare le password cifrate e leggibili solo dall'utente Windows che le ha salvate; nessuna password in chiaro su disco o nei log dell'app.
- **FR-007**: Gli utenti MUST poter creare, modificare, duplicare, eliminare e avviare/fermare sessioni.
- **FR-008**: Il sistema MUST permettere più sessioni attive contemporaneamente, ciascuna in una scheda, con possibilità di affiancarne almeno due nella stessa finestra.
- **FR-009**: Il sistema MUST mostrare per ogni sessione uno stato: connessione in corso, attiva, in pausa, disconnessa/riconnessione, errore (con messaggio), ferma.
- **FR-010**: Il sistema MUST colorare le righe in base al livello di log rilevato (ERROR/FATAL, WARN, INFO, DEBUG/TRACE).
- **FR-011**: Gli utenti MUST poter filtrare le righe visualizzate per testo o espressione regolare, senza interrompere la lettura.
- **FR-012**: Gli utenti MUST poter cercare testo con evidenziazione e navigazione tra occorrenze.
- **FR-013**: Il sistema MUST sospendere lo scroll automatico quando l'utente scorre verso l'alto e riprenderlo su richiesta o tornando in fondo.
- **FR-014**: Il sistema MUST rilevare rotazione/troncamento del file e continuare la lettura sul nuovo contenuto, segnalandolo.
- **FR-015**: Il sistema MUST ritentare automaticamente la connessione dopo una disconnessione e riprendere senza duplicare né perdere righe (salvo rotazione nel frattempo).
- **FR-016**: Il sistema MUST limitare le righe tenute in memoria per sessione (default 100.000, configurabile) scartando le più vecchie.
- **FR-017**: Il sistema MUST rilasciare le connessioni alle share aperte dall'app alla chiusura della sessione o dell'app, senza toccare mappature create dall'utente fuori dall'app.
- **FR-018**: Gli utenti MUST poter testare la connessione di una sessione dal form di configurazione prima di salvarla.
- **FR-019**: Gli utenti MUST poter copiare righe selezionate negli appunti.

### Key Entities

- **Sessione (configurazione)**: nome, share remota, nome utente, riferimento alla password cifrata, percorso file, righe iniziali, encoding. Persistita.
- **Sessione attiva**: istanza in esecuzione di una Sessione; stato, posizione di lettura nel file, buffer righe, filtro/ricerca correnti, stato follow.
- **Riga di log**: testo, livello rilevato, ordine di arrivo.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Da sessione salvata, l'utente vede le ultime 1000 righe del log remoto in meno di 5 secondi dal click (rete LAN).
- **SC-002**: Nuove righe scritte sul server appaiono nella vista entro 2 secondi nel 95% dei casi.
- **SC-003**: Con 5 sessioni attive in parallelo che ricevono ciascuna 100 righe/secondo, l'interfaccia resta reattiva (scroll, filtro, cambio scheda senza blocchi percepibili, < 200 ms).
- **SC-004**: Una nuova sessione si configura e salva in meno di 1 minuto; riavviarla richiede un solo click.
- **SC-005**: Dopo un'interruzione di rete di 30 secondi, la sessione riprende da sola senza righe duplicate.
- **SC-006**: Nessuna password rintracciabile in chiaro nei file salvati dall'app.

## Assumptions

- Uso personale su postazione Windows 10/11; un solo utente per installazione.
- Server raggiungibili via condivisione di rete Windows (SMB) dalla postazione; accesso remoto via altri protocolli (SSH, API, agent sul server) fuori scope v1.
- Accesso ai log in sola lettura: l'app non modifica né cancella file remoti.
- Un file per sessione; tail di più file con wildcard o cartelle intere fuori scope v1 (si aprono più sessioni).
- Livelli di log rilevati da parole chiave comuni nella riga (ERROR, WARN, INFO, DEBUG, ecc.); parsing di formati strutturati (JSON) fuori scope v1.
- Esportazione/salvataggio del log visualizzato su file locale fuori scope v1 (copia negli appunti disponibile).
- Interfaccia in italiano.
