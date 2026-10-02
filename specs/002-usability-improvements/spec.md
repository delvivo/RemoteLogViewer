# Feature Specification: Miglioramenti di usabilità (workspace, avvisi, contesto, guida)

**Feature Branch**: `feature/002-usability-improvements`

**Created**: 2026-09-30

**Status**: Draft

**Input**: User description: "Miglioramenti usabilità Remote Log Viewer: (1) ripristino workspace: all'avvio riapre le sessioni aperte alla chiusura, stesso ordine schede e modalità Affianca; (2) salva buffer su file (Ctrl+S): esporta righe visibili filtrate in un file .log locale; (6) contatori per livello (ERROR/WARN/INFO/DEBUG) nella barra di stato di ogni sessione; (7) avviso su schede in background: badge sul titolo della scheda quando arriva un ERROR in una sessione non visibile, e lampeggio taskbar se la finestra non ha il focus; (10) righe di contesto attorno ai match del filtro testo/regex (come grep -C N, N configurabile); (11) carica righe precedenti: pulsante per caricare altre N righe sopra quelle lette inizialmente. Inoltre: documentazione utente HTML (in italiano) inclusa nell'app e apribile dall'applicazione (es. pulsante Guida / F1), da aggiornare a ogni modifica che la riguarda."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Avviso di errori nelle sessioni non visibili (Priority: P1)

L'utente segue più sessioni in schede ma ne guarda una sola alla volta. Quando in una scheda non visibile arriva un errore, il titolo della scheda mostra un indicatore con il numero di errori arrivati da quando l'ha guardata l'ultima volta. Se l'app non è in primo piano (l'utente sta lavorando in un'altra finestra), il pulsante dell'app nella barra delle applicazioni lampeggia.

**Why this priority**: È il motivo principale per tenere aperte più sessioni: accorgersi subito di un problema senza controllare ogni scheda a mano.

**Independent Test**: Aprire due sessioni, guardare la prima, scrivere una riga ERROR nel file della seconda: il titolo della seconda scheda mostra "(1)". Selezionare la seconda scheda: l'indicatore sparisce. Ripetere con l'app in background: la barra delle applicazioni lampeggia.

**Acceptance Scenarios**:

1. **Given** due schede aperte e la scheda A visibile, **When** nella sessione B arrivano 3 nuove righe di livello ERROR, **Then** il titolo della scheda B mostra un indicatore con il numero 3, evidenziato in rosso.
2. **Given** la scheda B ha un indicatore, **When** l'utente seleziona la scheda B, **Then** l'indicatore sparisce e il conteggio riparte da zero.
3. **Given** la scheda visibile è A, **When** in A arriva un ERROR, **Then** nessun indicatore compare su A.
4. **Given** la modalità Affianca (tutte le sessioni visibili insieme), **When** in una qualsiasi sessione arriva un ERROR, **Then** nessun indicatore compare (tutte le sessioni sono già visibili).
5. **Given** la finestra dell'app non ha il focus, **When** in una qualsiasi sessione arriva un ERROR, **Then** il pulsante dell'app nella barra delle applicazioni lampeggia finché l'utente non riporta l'app in primo piano.
6. **Given** un errore con stack trace di 20 righe, **When** arriva, **Then** l'indicatore aumenta di 1 (un evento di errore), non di 20.
7. **Given** la lettura iniziale all'apertura di una sessione, **When** contiene righe ERROR, **Then** queste non fanno scattare indicatore né lampeggio (solo le righe arrivate dopo l'apertura contano).

---

### User Story 2 - Ripristino delle sessioni aperte all'avvio (Priority: P1)

L'utente chiude l'app a fine giornata con 5 schede aperte, in un certo ordine e magari in modalità Affianca. Alla riapertura l'app riapre automaticamente le stesse sessioni, nello stesso ordine, con la stessa modalità di visualizzazione, senza domande.

**Why this priority**: Evita di riaprire a mano ogni mattina lo stesso insieme di sessioni.

**Independent Test**: Aprire 3 sessioni, riordinarle, attivare Affianca, chiudere l'app, riaprirla: stesse 3 sessioni, stesso ordine, Affianca attivo.

**Acceptance Scenarios**:

1. **Given** l'app viene chiusa con le schede S1, S3, S2 aperte in quest'ordine, **When** l'app viene riaperta, **Then** vengono riaperte S1, S3, S2 nello stesso ordine e la scheda selezionata alla chiusura torna selezionata.
2. **Given** l'app viene chiusa in modalità Affianca, **When** viene riaperta, **Then** la modalità Affianca è attiva.
3. **Given** una sessione aperta era stata eliminata dalle sessioni salvate prima della chiusura, **When** l'app viene riaperta, **Then** quella sessione viene ignorata senza errori.
4. **Given** una sessione con data nel percorso aperta su "oggi", **When** l'app viene riaperta un altro giorno, **Then** la sessione si riapre sulla data del giorno corrente (continua a seguire "oggi"), senza chiedere la data.
5. **Given** una sessione con data nel percorso aperta su una data passata fissa, **When** l'app viene riaperta, **Then** la sessione si riapre sulla stessa data, senza chiedere la data.
6. **Given** la stessa sessione era aperta due volte (es. su due date diverse), **When** l'app viene riaperta, **Then** vengono riaperte entrambe le schede.
7. **Given** l'app viene chiusa senza sessioni aperte, **When** viene riaperta, **Then** nessuna sessione viene aperta.
8. **Given** una sessione ripristinata non riesce a connettersi (es. credenziali cambiate), **When** l'app si riapre, **Then** quella scheda mostra lo stato di errore esistente e le altre si aprono normalmente.

---

### User Story 3 - Righe di contesto attorno ai risultati del filtro (Priority: P2)

L'utente filtra per "Exception" ma vuole vedere anche cosa è successo subito prima e subito dopo. Imposta un numero di righe di contesto N: la vista mostra, per ogni riga che corrisponde al filtro, anche le N righe precedenti e le N successive, come `grep -C N`. Gruppi non contigui sono separati visivamente.

**Why this priority**: Il filtro oggi isola la riga ma perde la causa; il contesto evita di togliere il filtro e cercare a mano.

**Independent Test**: Buffer con 100 righe, una sola contiene "boom" alla riga 50; filtro "boom" con contesto 2: si vedono le righe 48–52, con la riga 50 distinguibile dalle righe di contesto.

**Acceptance Scenarios**:

1. **Given** un filtro testo attivo e contesto N=2, **When** la riga 50 corrisponde, **Then** sono visibili le righe 48–52; la riga corrispondente è distinguibile visivamente dalle righe di contesto.
2. **Given** due corrispondenze vicine (righe 50 e 53) con N=2, **When** si applica il filtro, **Then** le righe 48–55 compaiono una sola volta, senza duplicati, come un unico gruppo.
3. **Given** due corrispondenze lontane, **When** si applica il filtro, **Then** tra i due gruppi c'è un separatore visivo.
4. **Given** contesto N=0 (predefinito), **When** si applica il filtro, **Then** il comportamento è identico a oggi.
5. **Given** filtro con contesto attivo e Follow attivo, **When** arrivano nuove righe dopo una corrispondenza, **Then** le N righe successive alla corrispondenza compaiono man mano che arrivano.
6. **Given** il filtro per livello (checkbox) esclude DEBUG, **When** una riga DEBUG è entro N righe da una corrispondenza, **Then** resta nascosta (le checkbox di livello si applicano anche al contesto).
7. **Given** nessun filtro testo, **When** N > 0, **Then** il contesto non ha effetto.

---

### User Story 4 - Caricare righe precedenti (Priority: P2)

All'apertura l'app legge le ultime N righe del file (impostazione della sessione). Se servono righe più vecchie, l'utente preme un pulsante per caricare un altro blocco di righe precedenti, che compare sopra quelle già presenti senza interrompere il tail.

**Why this priority**: Oggi per vedere più storia bisogna modificare la sessione e riaprirla, perdendo il buffer attuale.

**Independent Test**: File con 5000 righe, sessione con 1000 righe iniziali: si vedono le righe 4001–5000; premere "carica precedenti": si vedono 3001–5000, la posizione di lettura in coda resta invariata e le nuove righe in arrivo continuano a comparire.

**Acceptance Scenarios**:

1. **Given** una sessione aperta con 1000 righe iniziali su un file di 5000 righe, **When** l'utente preme "carica precedenti", **Then** vengono aggiunte in testa le righe 3001–4000, nell'ordine corretto, senza duplicati né righe perse.
2. **Given** l'inizio del file è già stato caricato, **When** la vista viene mostrata, **Then** il pulsante è disabilitato (non c'è altro da caricare).
3. **Given** l'utente sta leggendo a metà della vista, **When** carica righe precedenti, **Then** la riga che stava guardando resta visibile nella stessa posizione (la vista non salta).
4. **Given** filtri attivi, **When** vengono caricate righe precedenti, **Then** anche queste rispettano i filtri e aggiornano i contatori per livello.
5. **Given** il file è stato ruotato o troncato dopo l'apertura (il nuovo file viene letto dall'inizio), **When** la vista si aggiorna, **Then** il pulsante è disabilitato: non ci sono righe precedenti nel file attuale.
6. **Given** il caricamento porterebbe il buffer oltre il limite di 100.000 righe, **When** l'utente preme il pulsante, **Then** vengono caricate solo le righe che stanno nel limite e l'utente ne è informato.
7. **Given** la sessione non è attiva (errore, riconnessione, attesa file, ferma), **When** la vista si aggiorna, **Then** il pulsante è disabilitato.

---

### User Story 5 - Salvare le righe visibili su file (Priority: P3)

L'utente ha isolato con i filtri le righe rilevanti di un incidente e vuole allegarle a un ticket. Con Ctrl+S (o un pulsante) salva su un file locale esattamente le righe visibili, nell'ordine in cui le vede.

**Why this priority**: Utile ma già approssimabile con selezione + Ctrl+C.

**Independent Test**: Applicare un filtro che mostra 10 righe, premere Ctrl+S, salvare: il file contiene esattamente quelle 10 righe.

**Acceptance Scenarios**:

1. **Given** una vista con filtri attivi, **When** l'utente preme Ctrl+S e sceglie un percorso, **Then** il file contiene esattamente le righe visibili, nell'ordine mostrato.
2. **Given** la finestra di salvataggio, **When** si apre, **Then** propone un nome file basato sul nome della sessione e su data/ora correnti, estensione `.log`.
3. **Given** il contesto righe è attivo, **When** si salva, **Then** vengono salvate anche le righe di contesto, e i separatori tra gruppi come riga `--`.
4. **Given** il salvataggio fallisce (percorso non scrivibile), **When** l'utente conferma, **Then** compare un messaggio di errore e la sessione non viene influenzata.
5. **Given** la vista è vuota, **When** l'utente preme Ctrl+S, **Then** non si apre la finestra di salvataggio (nulla da salvare).

---

### User Story 6 - Contatori per livello (Priority: P3)

Ogni sessione mostra quante voci ERROR, WARN, INFO e DEBUG sono presenti nel buffer, per capire a colpo d'occhio lo stato di un log.

**Why this priority**: Informazione di sintesi utile, ma secondaria rispetto agli avvisi.

**Independent Test**: File con 2 ERROR (uno con stack trace), 5 WARN, 10 INFO: i contatori mostrano ERROR 2, WARN 5, INFO 10, DEBUG 0.

**Acceptance Scenarios**:

1. **Given** una sessione aperta, **When** arrivano nuove righe, **Then** i contatori si aggiornano entro l'aggiornamento successivo della vista.
2. **Given** un errore con stack trace, **When** viene contato, **Then** conta come 1 ERROR.
3. **Given** l'utente preme "Pulisci", **When** il buffer viene svuotato, **Then** i contatori tornano a zero.
4. **Given** il buffer supera il limite e le righe più vecchie vengono scartate, **When** avviene lo scarto, **Then** i contatori riflettono solo le righe rimaste.
5. **Given** filtri attivi, **When** l'utente guarda i contatori, **Then** si riferiscono all'intero buffer, non solo alle righe visibili.

---

### User Story 7 - Guida utente consultabile dall'app (Priority: P2)

L'utente (anche un collega nuovo) vuole capire come funzionano collezioni, credenziali, data nel percorso, filtri e le altre funzioni. Con F1 o un pulsante "Guida" apre una guida in italiano, in formato pagina web, che descrive tutte le funzioni e le scorciatoie da tastiera. La guida funziona anche senza connessione a Internet.

**Why this priority**: L'app ha ormai molte funzioni non evidenti (segnaposto data, credenziali condivise, trascinamenti, scorciatoie); senza guida restano sconosciute.

**Independent Test**: Premere F1 dall'app: si apre la guida nel browser predefinito; scollegare la rete e ripetere: la guida si apre comunque.

**Acceptance Scenarios**:

1. **Given** l'app aperta, **When** l'utente preme F1 o il pulsante "Guida", **Then** la guida si apre nel browser predefinito del sistema.
2. **Given** nessuna connessione di rete, **When** l'utente apre la guida, **Then** la guida viene visualizzata completa (testo, stile, eventuali immagini).
3. **Given** la guida, **When** l'utente la legge, **Then** trova una sezione per ogni funzione dell'app e un elenco completo delle scorciatoie da tastiera.
4. **Given** la guida, **When** l'utente la apre, **Then** riporta la versione dell'app a cui si riferisce.
5. **Given** la guida non si può aprire (es. nessun browser configurato), **When** l'utente preme F1, **Then** compare un messaggio con il percorso del file della guida.

---

### Edge Cases

- Chiusura anomala dell'app (crash, arresto del PC): il ripristino usa l'ultimo stato salvato; è accettabile perdere modifiche alle schede avvenute dopo l'ultimo salvataggio.
- Ripristino di 20+ sessioni: le connessioni partono tutte, l'interfaccia resta utilizzabile mentre si connettono.
- Sessione ripristinata con credenziale condivisa eliminata nel frattempo: si apre con utente/password propri della sessione (comportamento esistente).
- Avvisi: rotazione del file con molte righe ERROR lette dall'inizio del nuovo file contano come nuove righe (sono effettivamente nuove).
- Cambio giorno automatico (sessione su "oggi"): le righe del nuovo file contano per avvisi e contatori.
- Contesto righe con regex non valida: resta il comportamento attuale del filtro regex non valido.
- Contesto righe e righe separatrici (es. "— nuovo giorno —"): le righe separatrici restano sempre visibili come oggi.
- Carica precedenti su un file con codifica multibyte: nessun carattere spezzato al confine del blocco caricato.
- Carica precedenti quando la prima riga del buffer è incompleta (il blocco iniziale è partito a metà riga): la riga viene ricostruita correttamente, senza duplicati.
- Carica precedenti dopo un cambio giorno: riguarda il file attualmente seguito.
- Salvataggio di 100.000 righe: completa senza bloccare l'app in modo percepibile.

## Requirements *(mandatory)*

### Functional Requirements

**Avvisi (US1)**

- **FR-001**: Il sistema DEVE mostrare sul titolo di ogni scheda non visibile il numero di voci ERROR arrivate da quando la scheda è stata visibile l'ultima volta, in modo evidente (colore rosso).
- **FR-002**: L'indicatore DEVE azzerarsi quando la scheda diventa visibile.
- **FR-003**: In modalità Affianca nessuna sessione DEVE mostrare l'indicatore.
- **FR-004**: Se la finestra non ha il focus, all'arrivo di una voce ERROR in qualsiasi sessione il sistema DEVE far lampeggiare il pulsante dell'app nella barra delle applicazioni fino al ritorno del focus.
- **FR-005**: Una voce di errore seguita dalle sue righe di continuazione (stack trace) DEVE contare come una sola voce ERROR, per indicatore e contatori.
- **FR-006**: Le righe lette all'apertura della sessione (lettura iniziale) NON DEVONO attivare indicatore né lampeggio.

**Ripristino workspace (US2)**

- **FR-007**: Alla chiusura il sistema DEVE salvare l'elenco delle schede aperte (sessione, data scelta o "segue oggi"), il loro ordine, la scheda selezionata e la modalità Affianca.
- **FR-008**: All'avvio il sistema DEVE riaprire automaticamente le schede salvate, nello stesso ordine e con la stessa modalità, senza chiedere la data.
- **FR-009**: Una sessione con data che seguiva "oggi" DEVE riaprirsi sulla data corrente; una con data fissa DEVE riaprirsi sulla stessa data.
- **FR-010**: Le schede che fanno riferimento a sessioni non più esistenti DEVONO essere ignorate senza errori.
- **FR-011**: Lo stato del workspace DEVE essere salvato insieme alle sessioni, senza richiedere configurazione.

**Righe di contesto (US3)**

- **FR-012**: Ogni sessione DEVE permettere di impostare un numero di righe di contesto N (0–50, predefinito 0) da mostrare prima e dopo ogni riga che corrisponde al filtro testo/regex.
- **FR-013**: Le righe corrispondenti DEVONO essere distinguibili dalle righe di contesto.
- **FR-014**: Gruppi di righe che si sovrappongono o sono contigui DEVONO essere uniti senza duplicati; gruppi non contigui DEVONO essere separati da un separatore visivo.
- **FR-015**: I filtri per livello DEVONO applicarsi anche alle righe di contesto.
- **FR-016**: Con Follow attivo, le righe di contesto successive a una corrispondenza DEVONO comparire man mano che arrivano.

**Carica precedenti (US4)**

- **FR-017**: Ogni sessione DEVE offrire un comando "carica righe precedenti" che aggiunge in testa al buffer un blocco di righe precedenti alla prima già caricata, pari al numero di righe iniziali configurato nella sessione.
- **FR-018**: Le righe caricate DEVONO essere contigue alle righe esistenti: nessun duplicato, nessuna riga persa, nessun carattere spezzato.
- **FR-019**: Il comando DEVE essere disabilitato quando l'inizio del file è già caricato o la sessione non è attiva.
- **FR-020**: Il caricamento NON DEVE interrompere il tail né spostare la posizione di lettura in coda, e NON DEVE far saltare la vista.
- **FR-021**: Il caricamento NON DEVE superare il limite di 100.000 righe del buffer; se il blocco viene ridotto l'utente DEVE esserne informato.
- **FR-022**: Le righe caricate NON DEVONO attivare indicatori né lampeggio, ma DEVONO aggiornare i contatori.

**Salvataggio (US5)**

- **FR-023**: Con Ctrl+S (o un pulsante) il sistema DEVE salvare su file locale esattamente le righe visibili nella vista, nell'ordine mostrato, in UTF-8.
- **FR-024**: Il nome proposto DEVE essere `<nome sessione>_<aaaaMMgg_HHmmss>.log`, con i caratteri non validi per un nome file sostituiti.
- **FR-025**: I separatori tra gruppi di contesto DEVONO essere salvati come riga `--`.
- **FR-026**: Un errore di scrittura DEVE essere mostrato all'utente senza effetti sulla sessione.

**Contatori (US6)**

- **FR-027**: Ogni sessione DEVE mostrare il numero di voci ERROR, WARN, INFO, DEBUG presenti nel buffer.
- **FR-028**: I contatori DEVONO riferirsi all'intero buffer (indipendentemente dai filtri), azzerarsi con "Pulisci" e diminuire quando righe vecchie vengono scartate per il limite del buffer.

**Guida (US7)**

- **FR-029**: L'app DEVE includere una guida utente in italiano, in formato HTML, che descrive tutte le funzioni e tutte le scorciatoie da tastiera.
- **FR-030**: La guida DEVE aprirsi nel browser predefinito con F1 (da qualsiasi punto della finestra principale) e con un pulsante "Guida" nella barra degli strumenti.
- **FR-031**: La guida DEVE essere consultabile senza connessione di rete (nessuna risorsa esterna).
- **FR-032**: La guida DEVE riportare la versione dell'app.
- **FR-033**: Se l'apertura fallisce, il sistema DEVE mostrare un messaggio con il percorso del file della guida.
- **FR-034**: Le istruzioni di progetto per gli sviluppatori DEVONO richiedere l'aggiornamento della guida a ogni modifica che cambia funzioni, comandi o scorciatoie visibili all'utente.

### Key Entities

- **Workspace**: stato dell'area di lavoro alla chiusura: elenco ordinato di schede aperte, scheda selezionata, modalità Affianca.
- **Scheda salvata**: riferimento a una sessione salvata + data scelta oppure "segue oggi" (solo per sessioni con data nel percorso).
- **Voce di log**: una riga con livello rilevato più le eventuali righe di continuazione che ne ereditano il livello; è l'unità contata da indicatori e contatori.
- **Guida**: documento HTML in italiano distribuito con l'app, con versione.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Dopo un riavvio, l'utente ritrova il proprio insieme di sessioni senza alcuna azione manuale (0 click), nel 100% dei casi in cui le sessioni esistono ancora.
- **SC-002**: Un errore in una sessione non visibile è segnalato all'utente entro 1 secondo dall'arrivo della riga nell'app.
- **SC-003**: Con il contesto attivo l'utente vede causa ed effetto di un errore filtrato senza togliere il filtro, in un solo passaggio.
- **SC-004**: Caricare 1.000 righe precedenti richiede meno di 2 secondi su una share di rete locale, senza bloccare le altre sessioni.
- **SC-005**: Salvare 100.000 righe visibili richiede meno di 3 secondi.
- **SC-006**: Ogni funzione dell'app e ogni scorciatoia da tastiera compare nella guida; la guida si apre in meno di 2 secondi anche offline.
- **SC-007**: Le funzioni esistenti (tail, rotazione, riconnessione, filtri, ricerca, collezioni, credenziali, data nel percorso) continuano a funzionare come prima; i test esistenti passano tutti.

## Assumptions

- La guida si apre nel browser predefinito di Windows (non in una finestra interna): nessuna dipendenza aggiuntiva.
- Il blocco di "carica precedenti" usa lo stesso numero delle righe iniziali della sessione; non serve un campo separato.
- Il numero di righe di contesto è un'impostazione della vista (non salvata nella sessione), come gli altri filtri attuali.
- Lo stato del workspace si salva alla chiusura normale dell'app; dopo un crash si ripristina l'ultimo stato salvato.
- Il lampeggio usa il comportamento standard di Windows (lampeggia finché la finestra non torna in primo piano); non c'è un'opzione per disattivarlo in questa versione.
- Le voci contate come ERROR sono quelle che il rilevamento livelli attuale classifica come ERROR (inclusi FATAL/CRITICAL se oggi rientrano in ERROR).
- Il limite di 100.000 righe del buffer resta invariato.
- La funzione "Pulisci" esiste già e azzera anche i contatori.
