# Research: Collezioni, riordino schede e percorsi con data

## R1 — Sintassi del segnaposto data

- **Decision**: `{date:<formato>}` con formato custom .NET (`yyyy`, `MM`, `dd`, `yy`, …). Regex `\{date:([^{}]+)\}`. Tutti i segnaposto di un percorso sono risolti con la stessa data. Ammesso solo nel percorso del file, non nella share.
- **Rationale**: il formato .NET è già quello di `DateTime.ToString`, quindi zero codice di parsing. La sintassi a graffe non collide con i caratteri validi nei percorsi Windows più comuni.
- **Validazione**: formato vuoto → errore; `ToString` che lancia `FormatException` → errore; risultato contenente `\ / : * ? " < > |` → errore (romperebbe il percorso). Formati con componenti orarie (`HH`, `mm`, `ss`) → errore: si sceglie solo una data.
- **Alternatives**: token fissi (`%Y%m%d`, strftime) richiedono un traduttore; una data "rilevata" automaticamente nel percorso è ambigua (`2026_06_11` vs numeri di versione).

## R2 — Scelta della data all'avvio

- **Decision**: `DateDialog` modale con `DatePicker` (default oggi), anteprima del percorso risolto aggiornata a ogni cambio, OK/Annulla. Mostrato solo se `DatePath.Has(FilePath)`.
- **Rationale**: `DatePicker` è in-box. Il dialog è modale come `SessionEditWindow`.
- **Alternatives**: una colonna/campo data nella sidebar è sempre visibile anche per le sessioni senza data (rumore); scegliere dall'elenco delle cartelle remote richiede una connessione prima dell'avvio (fuori scope).

## R3 — Cambio giorno a mezzanotte

- **Decision**: `ActiveSession` riceve `DateTime? Date`. `FollowToday = Date == DateTime.Today` al momento dell'avvio. A ogni iterazione del loop, se `FollowToday && DateTime.Today != Date`: `Date = DateTime.Today`, tailer = null, marker `— nuovo giorno: <data> —`, flag per leggere il nuovo file dall'inizio (`ReadTail(100_000)`, cap pari al buffer), `StateChanged` per aggiornare il titolo. Il file mancante segue già il ramo `Waiting`.
- **Rationale**: riusa il loop di polling (≤ 500 ms). Il risveglio dopo sospensione salta direttamente a oggi, come chiede la spec.
- **Alternatives**: `DispatcherTimer` sulla UI che riavvia la sessione perde il buffer e lo stato di follow; `SystemEvents.TimeChanged` non scatta a mezzanotte.

## R4 — Modello delle collezioni

- **Decision**: lista piatta `Collection { Id, Name, ParentId? }` + `SessionConfig.CollectionId?`. L'albero è derivato. Ordinamento: collezioni per nome, poi sessioni per nome (culture corrente, case-insensitive).
- **Rationale**: move = cambiare un ID; migrazione v1 gratuita (campo assente = radice); controllo dei cicli risalendo i `ParentId`; export = sottoinsieme delle stesse liste.
- **Alternatives**: JSON annidato (`children`) rende move/delete ricorsivi e l'import meno lineare; tag/percorso stringa (`"PROD/API"`) non permette collezioni vuote né rinomine atomiche.

## R5 — Formato di export/import

- **Decision**: file JSON `{ "format": "remote-log-viewer-collection", "version": 1, "collections": [...], "sessions": [...] }`, stessa forma di `sessions.json`, `protectedPassword` sempre null. Le radici del file hanno `parentId: null`. Import: ID rimappati a nuovi Guid, radici agganciate alla destinazione, nome con suffisso ` (2)`, ` (3)`… in caso di conflitto sullo stesso livello. Validazione completa prima di toccare l'albero (tutto o niente).
- **Rationale**: riusa il serializer. La rimappatura degli ID garantisce entità indipendenti (US4-5).
- **Alternatives**: zip o formato binario non servono; mantenere gli ID originali farebbe collidere due import dello stesso file.

## R6 — Drag & drop

- **Decision**: API WPF `DragDrop.DoDragDrop`. Schede: `PreviewMouseMove` su `TabItem` con tasto premuto oltre `SystemParameters.MinimumHorizontalDragDistance` → drag del `TabItem`; `Drop` su un altro `TabItem` → `Items.Remove`/`Insert` all'indice target, riselezione e `Relayout()`. Albero: stesso schema su `TreeViewItem`; target collezione o spazio vuoto (= radice); `SessionTree.CanMove` blocca i cicli. Alternativa da tastiera: menu contestuale "Sposta in…" con sottomenu delle collezioni.
- **Rationale**: nessuna libreria esterna. Il `LogView` resta nel `Tag` del `TabItem`, quindi la sessione non viene toccata dallo spostamento (FR-021).
- **Alternatives**: GongSolutions.WPF.DragDrop è una dipendenza in più per ~40 righe.

## R7 — Input del nome collezione

- **Decision**: `TextDialog` minimale (TextBlock + TextBox + OK/Annulla), riusato per "Nuova collezione" e "Rinomina".
- **Alternatives**: `Microsoft.VisualBasic.Interaction.InputBox` ha uno stile fuori tema e un riferimento extra; l'editing inline nel `TreeView` aggiunge più codice (template di edit, focus, Esc).
