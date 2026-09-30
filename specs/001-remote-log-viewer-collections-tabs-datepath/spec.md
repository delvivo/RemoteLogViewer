# Feature Specification: Collezioni, riordino schede e percorsi con data

**Feature Branch**: `feature/001-remote-log-viewer`

**Created**: 2026-09-30

**Status**: Draft

**Input**: User description: "Aggiungere la possibilità di creare collezioni (stile cartelle, anche annidate). Le collezioni devono poter essere esportabili ed importabili. Aggiungere la possibilità di spostare i tab per cambiarne l'ordine. Aggiungere inoltre la possibilità di gestire dinamicamente un path con date format specifico (e.g. abbiamo il path \\server\E$\DOB\DOB_SDL\log\standard_logs\2026_06_11\log.log, deve essere possibile gestire il campo data così da poter scegliere la data prima di avviare la sessione; in questo caso è una cartella, ma uguale se fosse nel nome del file)."

## Clarifications

### Session 2026-09-30

- Q: Come gestire le password salvate quando si esporta una collezione? → A: Escluse dall'export; chi importa le reinserisce.
- Q: Cosa si può esportare in un'unica operazione? → A: L'elemento selezionato: una collezione (con contenuto) oppure la radice (tutto).
- Q: Una sessione avviata su oggi e aperta dopo mezzanotte deve passare al file del nuovo giorno? → A: Sì, solo se la data scelta era oggi; le date passate restano fisse.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Percorso con data scelta all'avvio (Priority: P1)

Molti log sono organizzati per giorno: la data compare in una cartella (es. `...\standard_logs\2026_06_11\log.log`) o nel nome del file (es. `...\log_2026-06-11.log`). L'utente, nella configurazione della sessione, marca la parte di percorso che è una data e ne indica il formato (es. `yyyy_MM_dd`). Quando avvia la sessione, l'app chiede quale data aprire (proposta: oggi) e apre il file corrispondente.

**Why this priority**: Senza questa funzione l'utente deve modificare la sessione salvata ogni giorno o crearne una per data; è il limite più frequente nell'uso reale.

**Independent Test**: Configurare una sessione con segnaposto data nella cartella, avviarla scegliendo una data passata esistente, verificare che venga seguito il file di quella data; ripetere con la data nel nome del file.

**Acceptance Scenarios**:

1. **Given** una sessione il cui percorso contiene un segnaposto data con formato `yyyy_MM_dd`, **When** l'utente la avvia, **Then** l'app chiede la data (preimpostata a oggi) e mostra l'anteprima del percorso risultante prima di confermare.
2. **Given** l'utente sceglie l'11/06/2026, **When** conferma, **Then** la sessione segue `\\server\E$\DOB\DOB_SDL\log\standard_logs\2026_06_11\log.log`.
3. **Given** il segnaposto data è nel nome del file (es. `log_{data}.log`), **When** l'utente avvia la sessione, **Then** il comportamento è identico al caso cartella.
4. **Given** una sessione senza segnaposto data, **When** l'utente la avvia, **Then** non viene chiesta alcuna data (comportamento attuale invariato).
5. **Given** la data scelta produce un file inesistente, **When** la sessione parte, **Then** la sessione va nello stato "in attesa del file" già esistente, senza errore bloccante.
6. **Given** una sessione avviata con una data, **When** la scheda è aperta, **Then** il titolo della scheda mostra la data scelta, così da distinguere due schede della stessa sessione su date diverse.
7. **Given** una sessione avviata con data = oggi, **When** passa la mezzanotte (ora locale del PC), **Then** la sessione passa al file del nuovo giorno partendo dall'inizio del file, inserisce nella vista una riga separatrice con la nuova data e aggiorna il titolo della scheda; se il nuovo file non esiste ancora va in "in attesa del file".
8. **Given** una sessione avviata con una data passata, **When** passa la mezzanotte, **Then** la sessione resta sul file della data scelta.

---

### User Story 2 - Organizzare le sessioni in collezioni annidate (Priority: P1)

L'utente ha molte sessioni (ambienti, server, applicazioni). Le organizza in collezioni, come cartelle, anche annidate (es. `PROD > API > nodo1`). Può creare, rinominare ed eliminare collezioni, e spostare sessioni e collezioni da una collezione all'altra.

**Why this priority**: Con molte sessioni la lista piatta diventa ingestibile; è la base su cui poggia l'esportazione.

**Independent Test**: Creare collezione "PROD" con sottocollezione "API", spostarci due sessioni, chiudere e riaprire l'app, verificare che struttura e contenuto siano conservati e le sessioni avviabili.

**Acceptance Scenarios**:

1. **Given** l'elenco sessioni, **When** l'utente crea una collezione (alla radice o dentro un'altra), **Then** compare come nodo espandibile/comprimibile nell'albero.
2. **Given** una sessione o una collezione, **When** l'utente la trascina (o usa un comando "Sposta in…") su un'altra collezione o sulla radice, **Then** viene spostata lì con tutto il suo contenuto.
3. **Given** una collezione, **When** l'utente la rinomina, **Then** il nuovo nome persiste dopo il riavvio.
4. **Given** una collezione non vuota, **When** l'utente la elimina, **Then** l'app chiede conferma indicando quante sessioni verranno eliminate, ed elimina collezione e contenuto solo dopo conferma.
5. **Given** una collezione, **When** l'utente tenta di spostarla dentro sé stessa o dentro una sua discendente, **Then** l'operazione è impedita.
6. **Given** sessioni salvate prima di questa funzione, **When** l'app viene aggiornata, **Then** tutte le sessioni esistenti compaiono alla radice, senza perdita di dati.
7. **Given** una sessione in una collezione, **When** l'utente fa doppio click, **Then** si avvia come oggi (con eventuale richiesta data, vedi US1).

---

### User Story 3 - Riordinare le schede aperte (Priority: P2)

L'utente ha più sessioni aperte in schede e vuole metterle in un ordine utile (es. nodi dello stesso cluster vicini). Trascina una scheda in una nuova posizione.

**Why this priority**: Migliora l'uso quotidiano con molte schede, ma non blocca nessun flusso.

**Independent Test**: Aprire 3 sessioni, trascinare la terza in prima posizione, verificare il nuovo ordine sia nelle schede sia nella vista affiancata.

**Acceptance Scenarios**:

1. **Given** almeno due schede aperte, **When** l'utente trascina una scheda su un'altra posizione, **Then** la scheda viene spostata lì e resta selezionata.
2. **Given** schede riordinate, **When** l'utente attiva la vista affiancata, **Then** le sessioni appaiono nello stesso ordine delle schede.
3. **Given** una sessione attiva in una scheda, **When** la scheda viene spostata, **Then** la sessione continua senza interruzioni né perdita di righe o del filtro/stato di follow.

---

### User Story 4 - Esportare e importare collezioni (Priority: P2)

L'utente vuole condividere con un collega la propria configurazione (es. tutte le sessioni "PROD") o spostarla su un altro PC. Esporta una collezione (con sottocollezioni e sessioni) in un file; il collega la importa e la ritrova nel proprio albero.

**Why this priority**: Utile per team e cambio PC, ma dipende da US2.

**Independent Test**: Esportare una collezione con sottocollezioni in un file, importarla (anche su un altro utente Windows), verificare struttura e sessioni identiche, salvo le password come da regola sotto.

**Acceptance Scenarios**:

1. **Given** una collezione selezionata, **When** l'utente sceglie "Esporta…", **Then** l'app salva un file contenente la collezione, le sue sottocollezioni e tutte le sessioni incluse.
2. **Given** la radice selezionata (o nessuna selezione), **When** l'utente sceglie "Esporta…", **Then** il file contiene l'intero albero: tutte le collezioni e le sessioni alla radice.
3. **Given** un file esportato, **When** l'utente sceglie "Importa…" su una collezione di destinazione (o radice), **Then** la collezione importata compare lì con la stessa struttura; per un export della radice, collezioni e sessioni contenute vengono aggiunte direttamente nella destinazione.
4. **Given** la destinazione contiene già una collezione con lo stesso nome, **When** si importa, **Then** la collezione importata viene aggiunta come nuova (nome con suffisso, es. "PROD (2)") senza sovrascrivere nulla.
5. **Given** si importa due volte lo stesso file, **When** l'import termina, **Then** le sessioni importate sono entità distinte dalle esistenti (modificarne una non modifica l'altra).
6. **Given** un file non valido o corrotto, **When** l'utente lo importa, **Then** l'app mostra un errore comprensibile e non modifica l'albero esistente.
7. **Given** l'esportazione, **When** il file viene creato, **Then** le password non sono incluse; le sessioni importate hanno password vuota e l'utente la reinserisce modificando la sessione.

---

### Edge Cases

- Formato data non valido o vuoto nel segnaposto: la configurazione non si salva e viene indicato l'errore.
- Più segnaposto data nello stesso percorso (es. `2026\06\2026_06_11.log`): tutti vengono risolti con la stessa data scelta.
- Segnaposto data nella parte "share" del percorso: non ammesso (la share deve essere fissa), errore di validazione.
- Il file del giorno scelto non esiste ancora (es. data futura o log non ancora ruotato): stato "in attesa del file".
- La sessione resta aperta oltre la mezzanotte: se avviata su oggi passa al file del nuovo giorno (US1 scenario 7), altrimenti resta sulla data scelta.
- Il PC resta sospeso per più giorni con una sessione "oggi" aperta: al risveglio la sessione passa direttamente al file della data corrente, senza ripercorrere i giorni intermedi.
- Nome collezione vuoto o duplicato nello stesso livello: nome vuoto rifiutato; duplicato consentito solo tramite import con suffisso automatico, nella creazione manuale viene rifiutato.
- Spostamento di una collezione dove esiste già una sorella con lo stesso nome: la collezione spostata riceve il suffisso (es. "API (2)"), lo spostamento non viene bloccato.
- Eliminazione di una collezione che contiene sessioni attualmente aperte: le schede aperte continuano a funzionare (girano su una copia della configurazione).
- Import di un file prodotto da una versione futura con campi sconosciuti: i campi sconosciuti vengono ignorati.
- Trascinamento di una scheda nella vista affiancata: non richiesto; il riordino si fa dalle schede.

## Requirements *(mandatory)*

### Functional Requirements

**Percorso con data**

- **FR-001**: L'utente MUST poter inserire nel percorso del file uno o più segnaposto data con formato esplicito (es. `{date:yyyy_MM_dd}`), sia in una cartella sia nel nome del file.
- **FR-002**: L'editor di sessione MUST mostrare un'anteprima del percorso risolto con la data odierna e segnalare formati non validi.
- **FR-003**: All'avvio di una sessione con segnaposto, il sistema MUST chiedere la data (default: oggi) mostrando il percorso risultante, e permettere di annullare l'avvio.
- **FR-004**: Il sistema MUST risolvere tutti i segnaposto del percorso con la data scelta prima di connettersi. Se la data scelta è oggi, al cambio di giorno (ora locale) la sessione MUST passare al file della nuova data, dall'inizio, con riga separatrice nella vista; altrimenti segue il file scelto per tutta la sua durata.
- **FR-005**: Le sessioni senza segnaposto MUST avviarsi come oggi, senza richiesta data.
- **FR-006**: Il titolo della scheda MUST includere la data attualmente seguita per le sessioni con segnaposto.

**Collezioni**

- **FR-007**: L'utente MUST poter creare, rinominare ed eliminare collezioni, alla radice o dentro altre collezioni, senza limite pratico di profondità.
- **FR-008**: L'elenco sessioni MUST essere mostrato come albero con collezioni espandibili; le sessioni possono stare in una collezione o alla radice.
- **FR-009**: L'utente MUST poter spostare sessioni e collezioni tra collezioni (trascinamento e comando alternativo da menu/tastiera).
- **FR-010**: Il sistema MUST impedire di spostare una collezione dentro sé stessa o una sua discendente.
- **FR-011**: L'eliminazione di una collezione non vuota MUST richiedere conferma indicando il numero di sessioni coinvolte.
- **FR-012**: Struttura e contenuto delle collezioni MUST persistere tra i riavvii; i dati esistenti MUST essere migrati alla radice senza perdite.
- **FR-013**: Le operazioni esistenti sulle sessioni (avvia, nuova, modifica, duplica, elimina) MUST funzionare anche per sessioni dentro collezioni; "Nuova" e "Duplica" creano la sessione nella collezione selezionata.

**Esportazione / importazione**

- **FR-014**: L'utente MUST poter esportare l'elemento selezionato in un file scelto tramite finestra di salvataggio: una collezione (con sottocollezioni e sessioni) oppure la radice (intero albero, incluse le sessioni alla radice).
- **FR-015**: L'utente MUST poter importare un file esportato in una collezione a scelta o alla radice, conservando la struttura.
- **FR-016**: L'import MUST creare entità nuove e indipendenti e non sovrascrivere mai collezioni o sessioni esistenti; i conflitti di nome MUST essere risolti con suffisso.
- **FR-017**: Un file non valido MUST produrre un messaggio d'errore comprensibile senza modifiche all'albero.
- **FR-018**: Il file di esportazione MUST NOT contenere password (né in chiaro né cifrate); username e tutti gli altri parametri sono inclusi.

**Schede**

- **FR-019**: L'utente MUST poter riordinare le schede aperte trascinandole.
- **FR-020**: La vista affiancata MUST rispettare l'ordine delle schede.
- **FR-021**: Il riordino MUST NOT interrompere la sessione né perdere buffer, filtro o stato di follow.

### Key Entities

- **Collezione**: nome, collezione genitore (o radice), elenco ordinato di sottocollezioni e sessioni.
- **Sessione**: configurazione esistente (nome, share, credenziali, percorso, righe iniziali, codifica) più l'appartenenza a una collezione; il percorso può contenere segnaposto data.
- **Segnaposto data**: porzione del percorso con un formato data; risolta con la data scelta all'avvio.
- **File di esportazione**: rappresentazione portabile di una collezione con sottocollezioni e sessioni, con indicazione di versione del formato.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: L'utente apre il log di un giorno diverso da oggi in meno di 15 secondi, senza modificare la sessione salvata.
- **SC-002**: Con 50 sessioni organizzate in collezioni, l'utente trova e avvia una sessione nota in meno di 10 secondi.
- **SC-003**: Esportazione e reimportazione di una collezione con 3 livelli e 20 sessioni produce una struttura identica (nomi, gerarchia, parametri) nel 100% dei casi.
- **SC-004**: Il 100% delle sessioni salvate prima dell'aggiornamento resta disponibile e avviabile dopo l'aggiornamento.
- **SC-005**: Il riordino delle schede non causa alcuna perdita di righe nelle sessioni attive.

## Assumptions

- Sintassi del segnaposto data: `{date:<formato>}` con formato data standard (`yyyy`, `MM`, `dd`, ecc.); si considera solo la data, non l'ora.
- "Oggi" e il cambio giorno si basano sull'ora locale del PC che esegue l'app, non su quella del server.
- Una sessione appartiene a una sola collezione (modello a cartelle, niente collegamenti multipli).
- L'ordine degli elementi nell'albero è alfabetico (collezioni prima delle sessioni); l'ordinamento manuale nell'albero è fuori scope.
- L'ordine delle schede non viene salvato tra i riavvii (le schede aperte non sono persistite oggi).
- Selezione multipla di elementi da esportare è fuori scope: si esporta una collezione o la radice.
- Il file di esportazione è un file di testo leggibile, separato dal file di configurazione interno dell'app.
