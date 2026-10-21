# Feature Specification: Analisi dei log con l'IA tramite strumenti CLI

**Feature Branch**: `feature/003-ai-log-analysis`

**Created**: 2026-10-21

**Status**: Draft

**Input**: User description: "Consultare i log con l'IA tramite strumenti CLI esterni (Claude Code `claude -p`, Codex `codex exec`, OpenCode `opencode run`, Gemini/Antigravity `gemini -p`, più comando personalizzato modificabile). Nuova finestra \"Analisi IA\": l'utente sceglie uno o più tab aperti (righe visibili/filtrate di ciascun LogView) da passare alla CLI, sceglie la CLI (preset + comando custom configurabile e salvato), sceglie un prompt preset (es. \"Report errori\": riepilogo errori raggruppati per causa, frequenza, prima/ultima occorrenza, possibili cause e azioni) oppure scrive un prompt libero. La CLI viene eseguita come processo esterno con i log passati via stdin/file temporaneo; l'output viene mostrato nella finestra (streaming, annullabile) e può essere esportato (Markdown .md). Interazione one-shot (no chat multi-turno). UI localizzata it/en; guide e README aggiornati."

## Clarifications

### Session 2026-10-21

- Q: Quanto testo di log al massimo inviare allo strumento IA per ogni analisi? → A: 1 MB fisso complessivo, mantenendo le righe più recenti di ciascuna scheda.
- Q: Prima di inviare i log a uno strumento IA, l'utente deve dare un consenso esplicito? → A: Conferma la prima volta per ogni strumento (ricordata), più avviso fisso nella finestra.
- Q: Come va mostrata la risposta dell'IA nella finestra? → A: Testo grezzo a carattere fisso, selezionabile, più pulsante "Apri formattato" che apre la risposta come pagina HTML nel browser.
- Q: Oltre al Markdown (.md), l'esportazione deve permettere di salvare anche in HTML? → A: Sì: .md o .html, scelto dal tipo file nel dialogo di salvataggio.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Report degli errori generato dall'IA (Priority: P1)

L'utente ha una o più sessioni aperte e vuole capire rapidamente cosa sta andando storto. Apre la finestra "Analisi IA", spunta le schede da analizzare, sceglie lo strumento IA installato sul PC (es. Claude Code), lascia il prompt preimpostato "Report errori" e avvia. Il testo della risposta compare man mano nella finestra: errori raggruppati per causa, numero di occorrenze, prima e ultima occorrenza, possibili cause e azioni suggerite.

**Why this priority**: È il caso d'uso chiesto esplicitamente e il nucleo della funzione: senza l'esecuzione dello strumento IA sui log non esiste valore.

**Independent Test**: Aprire un file locale con alcuni ERROR ripetuti, aprire "Analisi IA", selezionare la scheda, scegliere uno strumento installato e "Report errori", avviare: nella finestra compare un report che cita gli errori presenti nel file.

**Acceptance Scenarios**:

1. **Given** due schede aperte e lo strumento scelto installato, **When** l'utente seleziona entrambe le schede e avvia "Report errori", **Then** lo strumento riceve le righe visibili di entrambe (ciascuna introdotta dal nome della scheda) e la risposta compare progressivamente nella finestra.
2. **Given** una scheda con filtri attivi (livello o testo), **When** viene analizzata, **Then** allo strumento vengono passate solo le righe visibili nella scheda, come per il salvataggio.
3. **Given** un'analisi in corso, **When** l'utente preme "Annulla", **Then** lo strumento viene terminato, la finestra indica "annullato" e il testo già ricevuto resta visibile.
4. **Given** nessuna scheda selezionata, **When** l'utente prova ad avviare, **Then** l'avvio non è possibile e la finestra lo indica.
5. **Given** l'analisi terminata, **When** l'utente lavora sulle schede (nuove righe, filtri), **Then** il risultato mostrato non cambia: l'analisi è su una fotografia presa all'avvio.

---

### User Story 2 - Esportare il report (Priority: P1)

Ottenuto il report, l'utente lo salva su file per allegarlo a un ticket o inviarlo al team.

**Why this priority**: Richiesto esplicitamente ("poterlo esportare"); senza export il report si perde alla chiusura della finestra.

**Independent Test**: Dopo un'analisi completata, premere "Esporta…", scegliere un percorso: il file Markdown contiene intestazione (data, strumento, schede, prompt) e il testo della risposta.

**Acceptance Scenarios**:

1. **Given** un'analisi completata, **When** l'utente esporta scegliendo il tipo Markdown o HTML, **Then** viene creato un file `.md` o `.html` (autonomo, apribile offline) con intestazione (data/ora, strumento, schede analizzate, prompt) seguita dalla risposta integrale.
2. **Given** nessuna risposta presente, **When** la finestra è aperta, **Then** "Esporta…" è disabilitato.
3. **Given** un'analisi completata, **When** l'utente usa "Copia", **Then** la risposta finisce negli appunti.
4. **Given** un'analisi completata con titoli, elenchi e tabelle Markdown, **When** l'utente preme "Apri formattato", **Then** il browser mostra la risposta formattata, senza bisogno di connessione internet.

---

### User Story 3 - Scegliere lo strumento e personalizzare il comando (Priority: P2)

L'utente usa uno strumento diverso (Codex, OpenCode, Antigravity CLI) o vuole cambiare le opzioni (modello, flag). Sceglie da un elenco di strumenti preimpostati oppure modifica il comando; la scelta e il comando personalizzato vengono ricordati alla riapertura dell'applicazione.

**Why this priority**: Gli strumenti disponibili variano da PC a PC; senza personalizzazione la funzione è inutilizzabile per chi non ha lo strumento predefinito.

**Independent Test**: Scegliere "Personalizzato", inserire un comando qualunque che legge dall'input e scrive in output (es. un comando di sistema che conta le righe), avviare: compare il suo output. Chiudere e riaprire l'app: il comando è ancora quello inserito.

**Acceptance Scenarios**:

1. **Given** l'elenco strumenti, **When** l'utente lo apre, **Then** trova almeno Claude Code, Codex, OpenCode, Antigravity CLI e Personalizzato, ciascuno con il comando mostrato e modificabile.
2. **Given** un comando modificato per uno strumento, **When** l'utente riapre l'app e la finestra, **Then** trova lo stesso strumento selezionato e lo stesso comando.
3. **Given** un comando modificato, **When** l'utente preme "Ripristina", **Then** torna il comando predefinito dello strumento.
4. **Given** uno strumento non installato o un comando inesistente, **When** l'utente avvia, **Then** compare un messaggio chiaro ("comando non trovato: …") senza blocchi dell'app.
5. **Given** lo strumento termina con errore, **When** l'esecuzione finisce, **Then** la finestra mostra lo stato di errore con il codice di uscita e il messaggio d'errore dello strumento.

---

### User Story 4 - Prompt libero (Priority: P2)

Oltre ai prompt preimpostati, l'utente scrive una domanda propria ("perché alle 10:32 il servizio X ha smesso di rispondere?") e la invia insieme ai log scelti.

**Why this priority**: Estende il caso d'uso del report a qualunque domanda sui log; costa poco una volta presente il flusso P1.

**Independent Test**: Scrivere una domanda libera su un log noto e avviare: la risposta tratta la domanda posta.

**Acceptance Scenarios**:

1. **Given** i prompt preimpostati, **When** l'utente ne sceglie uno, **Then** il suo testo compare nella casella del prompt e può essere modificato prima dell'avvio.
2. **Given** un prompt vuoto, **When** l'utente prova ad avviare, **Then** l'avvio non è possibile.
3. **Given** un prompt scritto o modificato, **When** l'utente riapre la finestra, **Then** trova l'ultimo prompt usato.

---

### Edge Cases

- **Log molto grandi** (fino a 100.000 righe per scheda): oltre un limite di dimensione viene inviata solo la parte più recente di ciascuna scheda; la finestra avvisa prima/durante l'esecuzione quante righe sono state escluse.
- **Scheda chiusa** dopo la selezione e prima dell'avvio: viene ignorata; scheda chiusa durante l'analisi: nessun effetto (fotografia già presa).
- **Finestra chiusa durante l'analisi**: lo strumento viene terminato.
- **Seconda analisi avviata** mentre una è in corso: non possibile finché la prima non termina o viene annullata.
- **Strumento che resta in attesa di input interattivo** (es. login richiesto): l'utente può annullare; il messaggio d'errore dello strumento viene mostrato.
- **Strumento che non produce output**: a fine esecuzione la finestra indica "nessuna risposta".
- **Caratteri non ASCII** nei log o nella risposta (accenti, simboli): trasmessi e mostrati correttamente.
- **Cambio lingua** con la finestra aperta: i testi dell'interfaccia si aggiornano; i prompt preimpostati già inseriti restano come sono.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Il sistema MUST offrire un comando "Analisi IA" (barra strumenti e scorciatoia da tastiera) che apre una finestra non modale, unica, che resta aperta mentre si usano le schede.
- **FR-002**: La finestra MUST elencare le schede aperte con una casella di selezione ciascuna (preselezionata la scheda attiva) e aggiornare l'elenco quando le schede vengono aperte o chiuse.
- **FR-003**: Il sistema MUST passare allo strumento, per ogni scheda selezionata, le righe attualmente visibili (stessi filtri del salvataggio), precedute da un'intestazione con il nome della scheda e il percorso del file.
- **FR-004**: Il sistema MUST limitare la quantità di log inviata (limite fisso di 1 MB di testo complessivo, ripartito tra le schede selezionate) mantenendo le righe più recenti di ciascuna scheda e indicare nella finestra quante righe sono state escluse.
- **FR-005**: Il sistema MUST offrire strumenti preimpostati (Claude Code, Codex, OpenCode, Antigravity CLI) e uno "Personalizzato", ciascuno con un comando modificabile e ripristinabile al predefinito.
- **FR-006**: Il sistema MUST ricordare tra un avvio e l'altro lo strumento selezionato, i comandi modificati e l'ultimo prompt usato.
- **FR-007**: Il sistema MUST offrire prompt preimpostati (almeno "Report errori" e "Riepilogo") che si inseriscono nella casella del prompt, modificabile o sostituibile con testo libero.
- **FR-008**: Il sistema MUST eseguire lo strumento come processo esterno senza finestra di console, passando prompt e log senza limiti dovuti alla lunghezza della riga di comando, in una cartella di lavoro temporanea vuota (rimossa al termine). I comandi preimpostati MUST avviare lo strumento in modalità non interattiva e in sola lettura o senza strumenti (nessuna scrittura/modifica di file, nessun comando eseguito); la lettura di altri file del PC non è esclusa da tutti gli strumenti e va documentata. Per il comando personalizzato il comportamento è responsabilità dell'utente.
- **FR-009**: Il sistema MUST mostrare l'output dello strumento progressivamente durante l'esecuzione, con lo stato (in corso, completato, annullato, errore con codice di uscita) e il tempo trascorso.
- **FR-010**: L'utente MUST poter annullare un'analisi in corso; il processo dello strumento (e i suoi sottoprocessi) viene terminato. Lo stesso avviene alla chiusura della finestra o dell'app.
- **FR-011**: In caso di errore dello strumento il sistema MUST mostrare il messaggio d'errore prodotto dallo strumento; in caso di comando non trovato, un messaggio localizzato che lo indica.
- **FR-012**: L'utente MUST poter esportare la risposta in un file Markdown (.md) o HTML autonomo (.html, stessa resa di "Apri formattato"), scelto dal tipo file nel dialogo di salvataggio, con intestazione (data/ora, strumento, schede, prompt) e copiarla negli appunti.
- **FR-012a**: Il sistema MUST offrire "Apri formattato", che apre la risposta (con la stessa intestazione dell'export) come pagina HTML autonoma nel browser predefinito, con titoli, grassetto, elenchi, tabelle e blocchi di codice resi; il resto del testo è mostrato com'è.
- **FR-013**: La finestra MUST informare in modo visibile che i log selezionati vengono inviati allo strumento IA scelto (e quindi, per gli strumenti online, al relativo servizio). Al primo avvio con ciascuno strumento il sistema MUST chiedere conferma esplicita; la conferma viene ricordata per quello strumento e, se rifiutata, l'analisi non parte.
- **FR-014**: Tutti i testi della finestra MUST essere localizzati in italiano e inglese; i prompt preimpostati MUST essere disponibili nella lingua attiva.
- **FR-015**: Le guide utente (it/en) e i README (it/en) MUST descrivere la funzione, la scorciatoia e i comandi preimpostati.

### Key Entities

- **Strumento IA**: nome visualizzato, comando predefinito, comando corrente (eventualmente modificato dall'utente).
- **Prompt preimpostato**: nome e testo nella lingua attiva.
- **Richiesta di analisi**: strumento, comando, prompt, schede selezionate con le righe catturate all'avvio, righe escluse per limite.
- **Risultato di analisi**: testo della risposta, stato, codice di uscita, messaggio d'errore, durata; esportabile.
- **Impostazioni IA**: strumento selezionato, comandi modificati, ultimo prompt, strumenti per cui l'invio è già stato confermato; salvate con le impostazioni dell'app, mai esportate con le sessioni.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Con uno strumento installato e configurato, l'utente ottiene un report errori di una scheda in non più di 4 interazioni (apri finestra, scegli schede, avvia, esporta), senza digitare comandi; al primo uso di uno strumento si aggiunge la sola conferma di consenso.
- **SC-002**: Il primo testo della risposta compare nella finestra appena lo strumento lo produce (nessuna attesa della fine dell'esecuzione per vedere output).
- **SC-003**: L'annullamento termina lo strumento entro 2 secondi e l'interfaccia resta reattiva durante tutta l'analisi.
- **SC-004**: Il file esportato si apre in qualunque visualizzatore Markdown (.md) o browser (.html) e contiene il 100% del testo della risposta mostrata.
- **SC-005**: Un comando non valido o uno strumento mancante produce sempre un messaggio comprensibile e mai un crash dell'app.

## Assumptions

- Gli strumenti CLI sono installati, nel PATH e già autenticati dall'utente; l'app non li installa né gestisce login o chiavi.
- Interazione one-shot: ogni avvio è indipendente, nessuna conversazione multi-turno.
- Nella finestra la risposta (tipicamente Markdown) è mostrata come testo grezzo a carattere fisso; la versione formattata è solo nel browser, tramite "Apri formattato".
- Limite di dimensione dei log inviati: 1 MB fisso (non configurabile), ripartito tra le schede selezionate mantenendo le righe più recenti.
- Prompt e log vengono consegnati tramite input standard e, per gli strumenti che non lo leggono, come file `input.md` nella cartella di lavoro temporanea.
- Gemini CLI è deprecato da Google: il preset Google è Antigravity CLI (`agy`), che non legge lo stdin di testo e riceve l'input da un file nella cartella di lavoro temporanea.
- I comandi preimpostati usano la modalità non interattiva e sola lettura/senza strumenti (vedi FR-008).
- Le impostazioni IA vengono salvate accanto alle altre impostazioni dell'app (`%APPDATA%\RemoteLogViewer`).
- I risultati della ricerca avanzata non sono una sorgente di input in questa versione (scelta dell'utente: schede selezionate).
