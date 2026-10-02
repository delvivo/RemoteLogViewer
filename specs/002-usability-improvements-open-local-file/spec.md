# Feature Specification: Apertura di un file log dal file system

**Feature Branch**: `feature/002-usability-improvements`

**Created**: 2026-10-01

**Status**: Draft

**Input**: User description: "Aprire un file log (.log, .txt, ecc.) dal file system locale: comando/pulsante \"Apri file…\" con dialogo di selezione file (anche drag&drop sulla finestra), il file si apre in una nuova scheda con le stesse funzioni delle sessioni remote (tail con Follow, filtri, ricerca, ricerca avanzata tra tab, righe precedenti, salva, avvisi), senza connessione SMB né credenziali."

## Clarifications

### Session 2026-10-01

- Q: Quante righe iniziali legge un file locale appena aperto? → A: Fisso, 1000 come le sessioni remote; altre righe con *▲ Precedenti*. Nessun campo di scelta.
- Q: All'avvio si riaprono anche i file locali aperti alla chiusura? → A: Sì, se il file esiste ancora; se manca viene saltato in silenzio, senza avvisi.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Aprire un file con il dialogo di selezione (Priority: P1)

L'utente ha un file di log sul proprio PC (o su un'unità già accessibile a Windows) e vuole leggerlo con gli stessi strumenti usati per i log remoti, senza creare una sessione salvata né inserire credenziali. Preme **Apri file…**, sceglie il file e questo si apre in una nuova scheda.

**Why this priority**: È il nucleo della funzione e da solo la rende utilizzabile.

**Independent Test**: Premere *Apri file…*, scegliere un `.log` locale: compare una scheda con il nome del file, le ultime righe sono visibili e, se si aggiungono righe al file, compaiono in coda (Follow).

**Acceptance Scenarios**:

1. **Given** l'app avviata, **When** l'utente preme *Apri file…* e sceglie un file, **Then** si apre una nuova scheda con il nome del file come titolo (percorso completo nel suggerimento) e viene mostrata la fine del file.
2. **Given** una scheda su un file locale, **When** altri programmi aggiungono righe al file, **Then** le nuove righe compaiono man mano e, con Follow attivo, la vista le segue.
3. **Given** il dialogo di selezione, **When** l'utente sceglie più file, **Then** ciascuno si apre in una scheda propria, nell'ordine scelto.
4. **Given** il dialogo di selezione, **When** l'utente lo annulla, **Then** non succede nulla.
5. **Given** un file già aperto in una scheda, **When** l'utente lo riapre, **Then** viene selezionata la scheda esistente invece di aprirne una seconda.
6. **Given** il dialogo, **When** si guardano i filtri tipo file, **Then** sono proposti *Log e testo* (`.log`, `.txt`) e *Tutti i file*, e l'utente può aprire qualunque file di testo.

---

### User Story 2 - Trascinare un file sulla finestra (Priority: P2)

L'utente trascina uno o più file da Esplora risorse sulla finestra dell'app, e si aprono come con il dialogo.

**Why this priority**: Comodo e veloce, ma il dialogo copre già il caso.

**Independent Test**: Trascinare un `.log` sulla finestra: si apre una scheda come nello scenario 1 della Story 1.

**Acceptance Scenarios**:

1. **Given** l'app aperta, **When** l'utente trascina un file sulla finestra e lo rilascia, **Then** il file si apre in una nuova scheda.
2. **Given** più file trascinati insieme, **When** vengono rilasciati, **Then** ciascuno si apre in una scheda.
3. **Given** il trascinamento di una cartella o di elementi non file, **When** vengono rilasciati, **Then** vengono ignorati senza errori (con un avviso se nessun elemento è utilizzabile).
4. **Given** un trascinamento interno all'app (riordino delle schede o spostamento nell'elenco sessioni), **When** avviene, **Then** il comportamento esistente non cambia.

---

### User Story 3 - Stesse funzioni delle sessioni remote (Priority: P1)

Una scheda su file locale si comporta come le altre: filtri per livello e testo/regex, contesto, ricerca nella scheda, carica righe precedenti, salva righe visibili, contatori per livello, avvisi di errore quando la scheda non è visibile, modalità Affianca, riordino per trascinamento e inclusione nella ricerca avanzata tra tutte le schede.

**Why this priority**: Senza la parità funzionale il file locale sarebbe una funzione a metà.

**Independent Test**: Aprire due file locali e una sessione remota, usare ricerca avanzata, filtri, *Precedenti* e *Salva…* su una scheda locale: tutto funziona come sulle remote.

**Acceptance Scenarios**:

1. **Given** una scheda su file locale, **When** l'utente usa filtri, contesto, ricerca, *Precedenti*, *Salva…*, **Then** il comportamento è identico a quello di una sessione remota.
2. **Given** schede locali e remote aperte, **When** si usa la ricerca avanzata tra tutti i tab, **Then** anche i file locali sono cercati e raggruppati per scheda.
3. **Given** una scheda locale non visibile, **When** nel file arriva un ERROR, **Then** il titolo mostra il badge e, se l'app non ha il focus, la barra delle applicazioni lampeggia.
4. **Given** un file locale che viene troncato o sostituito (rotazione), **When** accade, **Then** la lettura riparte dall'inizio, come per i file remoti.
5. **Given** il file locale non più raggiungibile (cancellato, unità scollegata), **When** accade, **Then** la scheda mostra lo stato di attesa/riconnessione e riprende quando il file torna disponibile.
6. **Given** un file locale, **When** si apre, **Then** non viene richiesta né usata alcuna credenziale e non viene creata alcuna connessione di rete gestita dall'app.

---

### User Story 4 - Riapertura all'avvio dei file locali (Priority: P3)

Alla chiusura l'app ricorda le schede aperte; i file locali aperti vengono riaperti al successivo avvio insieme alle sessioni remote, se ancora esistono.

**Why this priority**: Coerente con la riapertura delle sessioni, ma non indispensabile.

**Independent Test**: Aprire un file locale e una sessione, chiudere e riavviare: entrambe le schede tornano nello stesso ordine; se il file locale è stato eliminato nel frattempo, la scheda viene saltata senza errori.

**Acceptance Scenarios**:

1. **Given** una scheda su file locale aperta alla chiusura, **When** l'app riparte, **Then** la scheda viene riaperta nella stessa posizione, selezione e modalità Affianca incluse.
2. **Given** il file non esiste più all'avvio, **When** l'app riparte, **Then** la scheda viene saltata e le altre si aprono normalmente.

---

### Edge Cases

- File inesistente o non leggibile (permessi, bloccato in esclusiva): messaggio chiaro con il motivo, nessuna scheda aperta.
- File vuoto: la scheda si apre vuota e mostra le righe appena compaiono.
- File molto grande (centinaia di MB): si legge solo la fine (stesso numero di righe iniziali delle sessioni) e l'interfaccia resta reattiva; le righe precedenti si caricano a blocchi.
- File binario o con codifica diversa: codifica rilevata automaticamente (come per le sessioni), con BOM che prevale.
- Più file con lo stesso nome in cartelle diverse: i titoli restano distinguibili (percorso completo nel suggerimento, cartella aggiunta al titolo se serve).
- Percorso su unità di rete già accessibile da Windows (UNC o unità mappata): si apre con le credenziali dell'utente Windows corrente; l'app non apre né chiude connessioni.
- Cambio lingua con il dialogo o i messaggi visibili: i testi si aggiornano.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: L'app MUST offrire un comando **Apri file…** nella barra degli strumenti e una scorciatoia da tastiera (es. `Ctrl+O`).
- **FR-002**: Il comando MUST aprire il dialogo di selezione file di Windows, con selezione multipla e filtri *Log e testo* (`.log`, `.txt`) e *Tutti i file*.
- **FR-003**: Ogni file scelto MUST aprirsi in una nuova scheda, senza creare né salvare una sessione nell'elenco delle sessioni e senza chiedere credenziali o data.
- **FR-004**: L'app MUST accettare file trascinati sulla finestra e aprirli come da FR-003; cartelle ed elementi non file MUST essere ignorati senza errori.
- **FR-005**: Il trascinamento interno già esistente (riordino delle schede, spostamento nell'elenco sessioni) MUST continuare a funzionare invariato.
- **FR-006**: Aprendo un file già aperto in una scheda, l'app MUST selezionare la scheda esistente invece di duplicarla.
- **FR-007**: La scheda MUST mostrare il nome del file come titolo e il percorso completo come suggerimento; titoli uguali MUST essere distinguibili.
- **FR-008**: La scheda MUST seguire il file in tempo reale (Follow) e MUST mostrare inizialmente le ultime righe, nel numero fisso di 1000 (non configurabile; altre righe con *▲ Precedenti*).
- **FR-009**: La scheda MUST offrire tutte le funzioni delle sessioni remote: filtri per livello e testo/regex, contesto, ricerca con evidenziazione, contatori per livello, carica righe precedenti, salva righe visibili, avvio/stop, Pulisci, A capo.
- **FR-010**: Le schede su file locale MUST partecipare ad Affianca, al riordino per trascinamento, agli avvisi di errore (badge e lampeggio) e alla ricerca avanzata tra tutte le schede.
- **FR-011**: La lettura MUST gestire rotazione/troncamento, file temporaneamente assente (stato di attesa), codifica automatica e BOM come per i file remoti, senza bloccare altri programmi che scrivono sul file.
- **FR-012**: In caso di file non esistente o non leggibile, l'app MUST mostrare un messaggio chiaro e non lasciare schede vuote o in errore nascoste.
- **FR-013**: Le schede su file locale MUST essere ricordate alla chiusura e riaperte all'avvio, se il file esiste ancora; se manca, MUST essere saltate in silenzio, senza errori né avvisi.
- **FR-014**: L'app MUST NOT aprire, modificare o chiudere connessioni di rete per i file locali, né leggere o salvare credenziali.
- **FR-015**: Tutti i testi nuovi MUST essere disponibili in italiano e inglese, con cambio lingua dal vivo.
- **FR-016**: Guida utente (italiano e inglese), README e tabella delle scorciatoie MUST descrivere la nuova funzione.

### Key Entities

- **Sessione su file locale**: file letto dal file system; nome (dal file), percorso, codifica automatica, righe iniziali predefinite; non salvata nell'elenco sessioni; ricordata solo nello stato di riapertura (percorso).
- **Scheda**: la stessa dei log remoti, con in più l'origine (locale o remota).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: L'utente apre un file locale in meno di 10 secondi dal clic su *Apri file…* (o dal rilascio del file trascinato), senza inserire alcun dato oltre al file.
- **SC-002**: Un file da 500 MB mostra le ultime righe entro 3 secondi e l'interfaccia resta utilizzabile.
- **SC-003**: Le righe aggiunte al file compaiono nella scheda entro 2 secondi.
- **SC-004**: Il 100% delle funzioni elencate in FR-009 e FR-010 funziona su una scheda locale come su una remota.
- **SC-005**: Dopo la chiusura e il riavvio, il 100% dei file locali ancora esistenti viene riaperto nella stessa posizione.

## Assumptions

- «File system» include unità locali e percorsi di rete già accessibili a Windows con le credenziali dell'utente corrente; l'app non gestisce credenziali per questi percorsi (per quelli serve una sessione remota).
- Nessuna data nel percorso e nessun cambio-giorno automatico: la scheda segue il file così com'è.
- Righe iniziali e codifica sono quelle predefinite delle sessioni (1000, automatica); non sono configurabili in questa funzione.
- Un file locale non entra nell'elenco delle sessioni salvate né nelle esportazioni; la sua unica traccia persistente è la riapertura all'avvio.
- Fuori ambito: salvare un file locale come sessione, aprire cartelle intere, aprire archivi compressi (`.zip`, `.gz`).
