# Feature Specification: Ricerca avanzata tra tutti i tab aperti

**Feature Branch**: `feature/002-usability-improvements`

**Created**: 2026-10-01

**Status**: Draft

**Input**: User description: "Ricerca avanzata tra tutti i tab aperti: finestra/pannello unico che cerca testo o regex nel buffer di ogni sessione aperta; opzioni case sensitive e parola intera; filtri per livello (ERROR/WARN/...) e intervallo di timestamp; risultati raggruppati per tab, doppio click salta alla riga nel tab corrispondente; esportazione dei risultati su file."

## Clarifications

### Session 2026-10-01

- Q: Se la riga trovata è nascosta dai filtri della scheda, cosa fa il doppio click? → A: Azzera i filtri della scheda (livelli e testo), mostra la riga e avvisa "filtri rimossi".
- Q: Quanti risultati al massimo e cosa succede oltre il limite? → A: Max 10.000 risultati in totale; oltre, la ricerca si ferma e mostra "risultati limitati, restringi la ricerca".
- Q: Come si inserisce l'intervallo di data/ora e cosa succede con log che hanno solo l'orario? → A: Campi data+ora "da"/"a"; se la riga ha solo l'orario si usa la data della sessione (quella scelta nel dialogo data, altrimenti oggi).
- Q: Finestra separata o pannello agganciato? → A: Finestra separata non modale e ridimensionabile, che resta aperta mentre si usano le schede.
- Q: Uno stack trace conta come un risultato o una riga per corrispondenza? → A: Un risultato per ogni riga che corrisponde; le righe di stack trace ereditano il livello solo ai fini del filtro per livello e i conteggi sono per righe.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Cercare un testo in tutte le sessioni aperte (Priority: P1)

L'utente ha più sessioni aperte e deve capire dove compare una stringa (un id di transazione, un messaggio d'errore). Apre la ricerca avanzata, scrive il testo (o un'espressione regolare) e vede tutte le righe corrispondenti di tutte le sessioni aperte, raggruppate per scheda.

**Why this priority**: È il nucleo della funzione: oggi il filtro agisce su una sola sessione per volta.

**Independent Test**: Aprire due sessioni con righe diverse contenenti la stessa parola; cercarla: i risultati mostrano le righe di entrambe, sotto il nome di ciascuna scheda.

**Acceptance Scenarios**:

1. **Given** tre sessioni aperte e la parola presente in due di esse, **When** l'utente avvia la ricerca, **Then** i risultati mostrano un gruppo per ciascuna delle due sessioni con le righe trovate e il loro numero; la terza non compare.
2. **Given** la modalità regex attiva e un'espressione valida, **When** si cerca, **Then** vengono trovate le righe che la soddisfano.
3. **Given** la modalità regex attiva e un'espressione non valida, **When** si cerca, **Then** compare un messaggio di errore chiaro e i risultati precedenti non vengono alterati.
4. **Given** nessuna riga corrispondente, **When** si cerca, **Then** viene mostrato "nessun risultato".
5. **Given** una sessione chiusa o aperta dopo l'avvio della ricerca, **When** l'utente rilancia la ricerca, **Then** i risultati riflettono le sessioni aperte in quel momento.

---

### User Story 2 - Saltare alla riga trovata (Priority: P1)

Dall'elenco dei risultati l'utente fa doppio click su una riga e l'app seleziona la scheda corrispondente e mostra quella riga nel suo log.

**Why this priority**: Senza il salto un risultato non è utilizzabile nel contesto del log circostante.

**Independent Test**: Doppio click su un risultato di una scheda non visibile: la scheda viene selezionata e la riga appare visibile ed evidenziata.

**Acceptance Scenarios**:

1. **Given** un risultato di una scheda non selezionata, **When** doppio click, **Then** la scheda diventa attiva e la riga è visibile ed evidenziata.
2. **Given** la riga è nascosta dai filtri correnti della scheda (livello o testo), **When** doppio click, **Then** i filtri della scheda (livelli e testo) vengono azzerati, la riga è visibile ed evidenziata e compare un avviso "filtri rimossi".
3. **Given** la riga è stata rimossa dal buffer (oltre il limite di righe conservate), **When** doppio click, **Then** viene mostrato un messaggio che la riga non è più disponibile.
4. **Given** la modalità Affianca, **When** doppio click, **Then** la sessione corrispondente viene evidenziata e la riga mostrata.

---

### User Story 3 - Opzioni di corrispondenza e filtri per livello e orario (Priority: P2)

L'utente restringe la ricerca: maiuscole/minuscole significative, parola intera, solo certi livelli (ERROR, WARN...), solo un intervallo di data/ora.

**Why this priority**: Rende la ricerca utilizzabile su log voluminosi, ma il caso base funziona senza.

**Independent Test**: Cercare "error" con "maiuscole/minuscole" attivo e verificare che "ERROR" non corrisponda; impostare un intervallo orario e verificare che le righe fuori intervallo siano escluse.

**Acceptance Scenarios**:

1. **Given** "maiuscole/minuscole" attivo, **When** si cerca "Timeout", **Then** "timeout" non corrisponde.
2. **Given** "parola intera" attivo, **When** si cerca "err", **Then** "error" non corrisponde ma "err" isolato sì.
3. **Given** solo il livello ERROR selezionato, **When** si cerca, **Then** compaiono solo righe di livello ERROR (comprese le righe di stack trace dello stesso evento).
4. **Given** un intervallo orario "da" e/o "a", **When** si cerca, **Then** compaiono solo righe il cui timestamp rientra nell'intervallo (estremi inclusi).
5. **Given** righe senza timestamp riconoscibile, **When** è impostato un intervallo, **Then** ereditano il timestamp dell'ultima riga datata precedente della stessa sessione (come gli stack trace); se non ce n'è nessuna, sono escluse.
6. **Given** testo di ricerca vuoto ma filtri di livello/orario impostati, **When** si cerca, **Then** vengono elencate tutte le righe che soddisfano i filtri.

---

### User Story 4 - Esportare i risultati (Priority: P3)

L'utente salva su file i risultati trovati per allegarli a una segnalazione o analizzarli altrove.

**Why this priority**: Utile ma accessorio; i risultati sono già consultabili a video.

**Independent Test**: Dopo una ricerca, scegliere "Esporta" e aprire il file: contiene le righe trovate, con indicazione della sessione di provenienza.

**Acceptance Scenarios**:

1. **Given** risultati presenti, **When** l'utente esporta e sceglie un file, **Then** il file contiene tutte le righe dei risultati raggruppate per sessione, con il nome della sessione come intestazione di ciascun gruppo.
2. **Given** nessun risultato, **When** si guarda il comando, **Then** l'esportazione è disabilitata.
3. **Given** un file non scrivibile, **When** si esporta, **Then** viene mostrato un errore e nulla va perso.

---

### Edge Cases

- Nessuna sessione aperta: la ricerca è disponibile ma indica che non ci sono sessioni.
- Risultati molto numerosi (decine di migliaia): oltre 10.000 risultati in totale la ricerca si ferma e mostra l'avviso "risultati limitati, restringi la ricerca"; l'esportazione include solo i risultati mostrati.
- Nuove righe che arrivano durante la ricerca: i risultati sono un'istantanea al momento dell'avvio; una nuova ricerca li aggiorna.
- Sessioni in errore/riconnessione: i loro buffer già letti sono comunque ricercati.
- Buffer grandi (fino a 100.000 righe per sessione): l'interfaccia non si blocca e la ricerca può essere annullata.
- Cambio lingua con la finestra aperta: i testi si aggiornano.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: L'app MUST offrire una ricerca avanzata accessibile da un comando di menu/barra e da una scorciatoia da tastiera (es. Ctrl+Maiusc+F).
- **FR-002**: La ricerca MUST operare sulle righe già presenti nel buffer di tutte le sessioni aperte, indipendentemente dai filtri correnti di ogni scheda.
- **FR-003**: L'utente MUST poter scegliere tra testo semplice ed espressione regolare; un'espressione non valida MUST produrre un messaggio d'errore senza arresti anomali.
- **FR-004**: L'utente MUST poter attivare/disattivare "maiuscole/minuscole" e "parola intera" (di default insensibile alle maiuscole, parola intera disattivata).
- **FR-005**: L'utente MUST poter limitare la ricerca a uno o più livelli di log; di default tutti.
- **FR-006**: L'utente MUST poter limitare la ricerca a un intervallo di data/ora (campi data+ora "da" e "a", entrambi facoltativi); per le righe con solo l'orario, la data è quella della sessione (scelta nel dialogo data, altrimenti oggi).
- **FR-007**: Con testo vuoto e almeno un filtro attivo, la ricerca MUST elencare tutte le righe che soddisfano i filtri; senza testo né filtri MUST NOT elencare nulla.
- **FR-008**: I risultati MUST essere raggruppati per sessione/scheda, con nome della scheda e numero di corrispondenze per gruppo, e totale generale; i risultati MUST essere limitati a 10.000 in totale, con avviso quando il limite è raggiunto.
- **FR-008b**: Ogni riga che corrisponde è un risultato a sé (anche all'interno di uno stack trace); conteggi e limite di 10.000 sono per righe.
- **FR-009**: Ogni risultato MUST mostrare il testo della riga, con la corrispondenza evidenziata, e il livello.
- **FR-010**: Il doppio click su un risultato MUST selezionare la scheda della sessione e portare in vista la riga corrispondente, evidenziandola; se la riga è nascosta dai filtri della scheda, i filtri MUST essere azzerati con avviso all'utente.
- **FR-011**: Se la riga non è più nel buffer, l'app MUST informare l'utente senza errori.
- **FR-012**: L'utente MUST poter esportare i risultati in un file di testo (UTF-8 senza BOM), raggruppati per sessione.
- **FR-013**: La ricerca MUST NOT bloccare l'interfaccia né interferire con la lettura in tempo reale dei log, e MUST poter essere annullata o rilanciata.
- **FR-014**: La ricerca MUST aprirsi in una finestra separata, non modale e ridimensionabile, che resta aperta mentre l'utente usa le schede (anche in modalità Affianca); una sola istanza alla volta.
- **FR-015**: Tutti i testi della funzione MUST essere disponibili in italiano e inglese, con cambio lingua dal vivo.
- **FR-016**: La guida utente (italiano e inglese), il README e la tabella delle scorciatoie MUST descrivere la nuova funzione.

### Key Entities

- **Criteri di ricerca**: testo, modalità (semplice/regex), maiuscole/minuscole, parola intera, livelli ammessi, intervallo di data/ora.
- **Risultato**: riga trovata, sessione di provenienza, livello, posizione della riga nel buffer della sessione, evidenziazione della corrispondenza.
- **Gruppo di risultati**: insieme dei risultati di una sessione, con nome della scheda e conteggio.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: L'utente trova una stringa presente in una qualsiasi di 5 sessioni aperte in meno di 15 secondi dall'apertura della ricerca, senza cambiare scheda.
- **SC-002**: Con 5 sessioni da 100.000 righe ciascuna, i primi risultati compaiono entro 3 secondi e l'interfaccia resta utilizzabile durante la ricerca.
- **SC-003**: Dal doppio click la riga corrispondente è visibile nella scheda giusta in meno di 1 secondo, nel 100% dei casi in cui la riga è ancora nel buffer.
- **SC-004**: Il 100% delle righe del file esportato coincide con i risultati mostrati.
- **SC-005**: La combinazione di testo, livello e intervallo orario restituisce solo righe che soddisfano tutti i criteri (nessun falso positivo in test).

## Assumptions

- La ricerca agisce sui buffer in memoria (righe già lette, max 100.000 per sessione); non rilegge i file remoti. Per cercare più indietro l'utente usa "Carica righe precedenti" e rilancia la ricerca.
- Il timestamp di una riga è quello riconoscibile all'inizio della riga nei formati di log più comuni; le righe senza timestamp ereditano quello della riga datata precedente.
- La ricerca è un'istantanea: non si aggiorna da sola con le nuove righe, si rilancia manualmente.
- L'intervallo orario è interpretato nello stesso fuso dei timestamp del log, senza conversione.
- Fuori ambito: sostituzione di testo, ricerca nei file non aperti, salvataggio delle ricerche recenti.
