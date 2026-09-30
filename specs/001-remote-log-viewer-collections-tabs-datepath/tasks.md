---

description: "Task list for collections, tab reordering and date paths"
---

# Tasks: Collezioni, riordino schede e percorsi con data

**Input**: Design documents from `specs/001-remote-log-viewer-collections-tabs-datepath/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md)

**Tests**: xUnit sulla logica non-UI (`DatePath`, `SessionTree`, `SessionStore`, cambio giorno di `ActiveSession`), come richiesto dal plan. UI validata con quickstart.md.

**Organization**: una fase per user story; US4 dipende da US2.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallelizzabile (file diversi, nessuna dipendenza aperta)
- **[Story]**: US1…US4 da spec.md

---

## Phase 1: Setup

- [X] T001 Verificare la baseline: `taskkill //IM RemoteLogViewer.exe //F` se in esecuzione, poi `dotnet build` e `dotnet test` verdi dalla root del repo; backup di `%APPDATA%\RemoteLogViewer\sessions.json` in `sessions.json.pre-feature`

---

## Phase 2: Foundational

Nessun prerequisito condiviso: le user story toccano parti diverse. `SessionConfig.cs` e `MainWindow.xaml(.cs)` sono condivisi, quindi i task di storie diverse su questi file vanno eseguiti in sequenza, non in parallelo.

---

## Phase 3: User Story 1 - Percorso con data scelta all'avvio (Priority: P1) 🎯 MVP

**Goal**: segnaposto `{date:<fmt>}` nel percorso del file; data scelta all'avvio; cambio giorno automatico se la data scelta è oggi.

**Independent Test**: quickstart scenari 1–8.

### Tests (US1)

- [X] T002 [P] [US1] Creare `tests/RemoteLogViewer.Tests/DatePathTests.cs`. Casi:
  - `Has` true/false.
  - `Resolve(@"a\{date:yyyy_MM_dd}\log.log", 2026-06-11)` → `a\2026_06_11\log.log`.
  - Due segnaposto risolti con la stessa data.
  - Segnaposto nel nome del file `log_{date:yyyy-MM-dd}.log`.
  - `Validate` restituisce errore per: formato vuoto `{date:}`; risultato con caratteri `\ / : * ? " < > |` (es. `{date:yyyy/MM}`); componenti orarie (`HH`, `hh`, `mm`, `ss`, `fff`, `tt`); percorso senza segnaposto → nessun errore.
- [X] T003 [P] [US1] Creare `tests/RemoteLogViewer.Tests/SessionConfigTests.cs` con test di `SessionConfig`:
  - `ResolvePath(date)` combina share + file risolto.
  - `Validate()` segnala `"La data non è ammessa nella share."` quando `SharePath` contiene `{date:`.
  - `Validate()` include gli errori di `DatePath.Validate(FilePath)`.
- [X] T004 [P] [US1] Aggiungere a `tests/RemoteLogViewer.Tests/ActiveSessionTests.cs` un test del cambio giorno, con due cartelle di date consecutive sotto una temp dir raggiungibile via `\\localhost\C$` (early-return se non raggiungibile, come i test esistenti). Si avvia con `Date` = giorno 1, orologio iniettato `Today` = giorno 1, e poi si porta `Today` a giorno 2. Atteso:
  - marker `— nuovo giorno: yyyy-MM-dd —` in `Pending`;
  - righe del file del giorno 2 lette dall'inizio;
  - `DisplayName` con la nuova data.

  Aggiungere anche un caso con `Date` passata (≠ Today): nessun cambio.

### Implementation (US1)

- [X] T005 [P] [US1] Creare `src/RemoteLogViewer/DatePath.cs`: classe statica con regex `\{date:([^{}]*)\}` e i metodi:
  - `Has(string path)`;
  - `Resolve(string path, DateTime date)`: `date.ToString(fmt, CultureInfo.InvariantCulture)` per ogni match;
  - `Validate(string path)`: `List<string>` con messaggi italiani:
    - `"Formato data vuoto."`;
    - `"Formato data non valido: {fmt}."` su `FormatException`;
    - `"Il formato data non può contenere ore/minuti/secondi."` se il formato contiene `H h m s f t` fuori da quote;
    - `"Il formato data produce caratteri non ammessi nel percorso."` se `Resolve` con una data campione contiene `\ / : * ? " < > |`.
- [X] T006 [US1] Modificare `src/RemoteLogViewer/SessionConfig.cs`:
  - aggiungere `public string ResolvePath(DateTime date)` (stessa logica di `FullPath` ma su `DatePath.Resolve(FilePath, date)`);
  - lasciare `FullPath` non risolto per la visualizzazione;
  - in `Validate()` aggiungere `DatePath.Validate(FilePath)` e l'errore `"La data non è ammessa nella share."` se `DatePath.Has(SharePath)`;
  - il controllo "UNC dentro la share" deve usare il percorso risolto con `DateTime.Today`.
- [X] T007 [US1] Modificare `src/RemoteLogViewer/ActiveSession.cs`:
  - costruttore `ActiveSession(SessionConfig config, DateTime? date = null, Func<DateTime>? today = null)`;
  - proprietà `Date`, `FollowToday` (= `date == today()` all'avvio), `DisplayName` (`Name` oppure `$"{Name} · {Date:yyyy-MM-dd}"`) e `Path` (percorso risolto corrente, o `Config.FullPath` se `Date` è null);
  - in `Run`, all'inizio di ogni iterazione dopo la connessione: se `FollowToday && today() != Date`, allora `Date = today()`, marker `— nuovo giorno: {Date:yyyy-MM-dd} —`, `tailer = null`, flag `fromStart = true`, `StateChanged` (per il titolo);
  - la creazione del tailer usa `Path` e `ReadTail(fromStart ? 100_000 : Config.TailLines)`, poi resetta il flag;
  - il file assente ricade nel ramo `Waiting` esistente.
- [X] T008 [P] [US1] Creare `src/RemoteLogViewer/DateDialog.xaml` + `.xaml.cs`:
  - titolo `Avvia "<nome>"`, `DatePicker` (SelectedDate = oggi), TextBlock `Anteprima: <config.ResolvePath(data)>` aggiornato su `SelectedDateChanged`;
  - OK (`IsDefault`) / Annulla (`IsCancel`), `SizeToContent`, `WindowStartupLocation=CenterOwner`;
  - espone `DateTime SelectedDate`; OK disabilitato se non c'è una data.
- [X] T009 [US1] Modificare `src/RemoteLogViewer/MainWindow.xaml.cs` `Open(SessionConfig)`:
  - se `DatePath.Has(config.FilePath)` mostrare `DateDialog` (Annulla → return) e creare `new ActiveSession(config.Clone(), dlg.SelectedDate)`;
  - titolo della scheda da `session.DisplayName`, tooltip da `session.Path`;
  - su `view.StatusChanged` aggiornare anche testo e tooltip della scheda (cambio giorno);
  - in `Relayout()` il titolo della cella affiancata usa `v.Session.DisplayName`.
- [X] T010 [US1] Modificare `src/RemoteLogViewer/SessionEditWindow.xaml` + `.xaml.cs`:
  - sotto il campo File, TextBlock grigio `Anteprima: <ResolvePath(DateTime.Today)>` visibile solo con segnaposto, aggiornato a ogni modifica del campo;
  - hint `Usa {date:yyyy_MM_dd} per una data variabile` nel tooltip del campo;
  - il "test connessione" (righe ~59-60, `File.Exists(c.FullPath)`) deve usare `c.ResolvePath(DateTime.Today)`.
- [X] T011 [US1] Eseguire `dotnet test --filter "FullyQualifiedName~DatePathTests|FullyQualifiedName~SessionStoreTests|FullyQualifiedName~ActiveSessionTests"` e gli scenari 1–8 di quickstart.md

**Checkpoint**: US1 utilizzabile da sola sulla lista piatta attuale.

---

## Phase 4: User Story 2 - Collezioni annidate (Priority: P1)

**Goal**: albero di collezioni, persistito e migrato, con spostamento via drag & drop e "Sposta in…".

**Independent Test**: quickstart scenari 9–12.

### Tests (US2)

- [X] T012 [P] [US2] Creare `tests/RemoteLogViewer.Tests/SessionTreeTests.cs`. Casi:
  - `Children(null)` ordina le collezioni per nome, poi le sessioni per nome (case-insensitive);
  - `CanMove` false su sé stessa e su una discendente, true altrove;
  - `Move` di una sessione e di una collezione;
  - `CountSessions` ricorsivo;
  - `Delete` rimuove sottocollezioni e sessioni;
  - `UniqueName` → `X`, `X (2)`, `X (3)`;
  - `Normalize()` (vedi T014): `parentId`/`collectionId` orfani → radice; un ciclo → la collezione che lo chiude va alla radice.
- [X] T013 [P] [US2] Aggiungere a `tests/RemoteLogViewer.Tests/SessionStoreTests.cs`:
  - un file v1 (solo `sessions`, senza `collectionId`) si carica con collezioni vuote e sessioni alla radice;
  - round-trip v2 con `collections` e `collectionId`.

### Implementation (US2)

- [X] T014 [US2] Creare `src/RemoteLogViewer/SessionTree.cs`:
  - `public class Collection { Guid Id; string Name; Guid? ParentId }`, con proprietà `{ get; set; }`;
  - `public class SessionTree(List<Collection> collections, List<SessionConfig> sessions)` con i metodi `Children(Guid? parentId)`, `CanMove(Guid collectionId, Guid? newParentId)`, `Move(Collection c, Guid? parent)`, `Move(SessionConfig s, Guid? parent)`, `CountSessions(Guid id)`, `CountCollections(Guid id)`, `Delete(Guid id)`, `UniqueName(Guid? parentId, string name)`, `NameTaken(Guid? parentId, string name, Guid? exceptId)` (case-insensitive tra le collezioni sorelle), `Descendants(Guid id)`, `PathOf(Guid id)` (`"PROD › API"`), `Normalize()`.
  - Invarianti dal data-model: `Name` "obbligatorio, trim; univoco tra le collezioni sorelle (case-insensitive) nella creazione/rinomina manuale"; `ParentId` "deve riferire una collezione esistente"; "niente cicli".
- [X] T015 [US2] Modificare `src/RemoteLogViewer/SessionConfig.cs`: aggiungere `public Guid? CollectionId { get; set; }` (null = radice).
- [X] T016 [US2] Modificare `src/RemoteLogViewer/SessionStore.cs`:
  - `SessionsFile(List<SessionConfig> Sessions, List<Collection>? Collections)`;
  - `Load()` restituisce `SessionTree` (collezioni null → lista vuota) e chiama `Normalize()`;
  - `Save(SessionTree)` scrive entrambe le liste;
  - scrittura atomica e `.bak` invariati;
  - aggiornare i chiamanti e i test esistenti in `tests/RemoteLogViewer.Tests/SessionStoreTests.cs`.
- [X] T017 [P] [US2] Creare `src/RemoteLogViewer/TextDialog.xaml` + `.xaml.cs`:
  - label, TextBox (focus e selezione all'apertura), OK/Annulla;
  - `static string? Ask(Window owner, string title, string label, string initial = "")`;
  - OK con testo vuoto dopo il trim → messaggio `"Nome obbligatorio."`.
- [X] T018 [US2] Modificare `src/RemoteLogViewer/MainWindow.xaml` e `MainWindow.xaml.cs`:
  - sostituire il `ListBox SessionList` con il `TreeView SessionTreeView`, con template per i nodi collezione (icona 📁 + nome) e sessione (template attuale nome + `FullPath`);
  - nodi costruiti da una classe interna `Node { object Item; ObservableCollection<Node> Children; bool IsExpanded }`, ricostruiti da `SessionTree.Children` dopo ogni modifica (`RebuildTree()`);
  - espansione conservata tramite `HashSet<Guid> _expanded`, legato a `TreeViewItem.IsExpanded` via `ItemContainerStyle`; selezione ripristinata per ID;
  - `Selected` → sessione o collezione; `Save()` → `_store.Save(_tree)`.
- [X] T019 [US2] In `src/RemoteLogViewer/MainWindow.xaml(.cs)` adattare le operazioni esistenti:
  - Nuova/Duplica mettono la sessione nella collezione selezionata (o nella collezione della sessione selezionata);
  - Modifica conserva `CollectionId`;
  - Elimina su una sessione resta com'è;
  - doppio click / Invio su una sessione → `Open`, su una collezione → espandi/comprimi;
  - Canc = Elimina, F2 = Modifica/Rinomina.
- [X] T020 [US2] In `src/RemoteLogViewer/MainWindow.xaml(.cs)` aggiungere le operazioni sulle collezioni:
  - pulsante "Collezione" e voci di menu: Nuova collezione / Nuova sottocollezione, Rinomina con `TextDialog`;
  - errore `"Esiste già una collezione con questo nome."` se `NameTaken`;
  - Elimina collezione con conferma `Eliminare la collezione "X" con N sessioni e M sottocollezioni?`, oppure `Eliminare la collezione "X"?` se vuota.
- [X] T021 [US2] In `src/RemoteLogViewer/MainWindow.xaml(.cs)` aggiungere il `ContextMenu` del TreeView, costruito in `ContextMenuOpening` in base al nodo sotto il mouse, con le voci di [contracts/ui.md](contracts/ui.md).
  - "Sposta in…": sottomenu con "(radice)" + tutte le collezioni via `PathOf`, disabilitate dove `!CanMove`.
  - Le voci Esporta/Importa si aggiungono in US4.
- [X] T022 [US2] In `src/RemoteLogViewer/MainWindow.xaml.cs` aggiungere il drag & drop del TreeView:
  - `PreviewMouseLeftButtonDown` registra il punto e `PreviewMouseMove` oltre `SystemParameters.MinimumHorizontalDragDistance` avvia `DragDrop.DoDragDrop(node.Item)`;
  - `DragOver` imposta `DragDropEffects.None` se il target non è valido (`CanMove` false o target = sessione); `Drop` su collezione → `Move` dentro, su spazio vuoto → radice;
  - poi `Save()` e `RebuildTree()`, espandendo il target.
- [X] T023 [US2] Eseguire `dotnet test --filter "FullyQualifiedName~SessionTreeTests|FullyQualifiedName~SessionStoreTests"` e gli scenari 9–12 di quickstart.md

**Checkpoint**: US1 + US2 funzionanti insieme.

---

## Phase 5: User Story 3 - Riordinare le schede (Priority: P2)

**Goal**: drag & drop delle schede; "Affianca" rispetta l'ordine.

**Independent Test**: quickstart scenario 17.

- [X] T024 [US3] In `src/RemoteLogViewer/MainWindow.xaml.cs` `Open()`, sul `TabItem`:
  - `PreviewMouseLeftButtonDown` salva il punto; `PreviewMouseMove` con tasto premuto oltre la soglia → `DragDrop.DoDragDrop(tab, tab, Move)`; `AllowDrop = true`;
  - `Drop`: se il dato è un `TabItem` diverso, `Tabs.Items.Remove(src)`, `Tabs.Items.Insert(Tabs.Items.IndexOf(target), src)`, `Tabs.SelectedItem = src`, `Relayout()`;
  - non avviare il drag se il click parte dal pulsante ✕ (`e.OriginalSource` dentro un `Button`);
  - verificare che `LogView` (nel `Tag`) non venga ricreato né fermato.
- [X] T025 [US3] Eseguire lo scenario 17 di quickstart.md: 3 schede con righe in arrivo, riordino, poi "Affianca" → stesso ordine; nessuna riga persa; filtro e follow invariati

---

## Phase 6: User Story 4 - Esportare e importare collezioni (Priority: P2)

**Goal**: export della collezione selezionata o della radice, senza password; import tutto-o-niente con ID nuovi e nomi univoci.

**Independent Test**: quickstart scenari 13–16.

**Depends on**: US2 (T014–T021).

### Tests (US4)

- [X] T026 [P] [US4] Aggiungere a `tests/RemoteLogViewer.Tests/SessionTreeTests.cs`:
  - `Export(id)` include discendenti e sessioni, la radice esportata ha `ParentId` null e ogni `ProtectedPassword` è null;
  - `Export(null)` include tutto;
  - `Import` due volte → ID tutti diversi dagli originali e tra i due import, nomi `PROD` e `PROD (2)`;
  - import sotto la destinazione;
  - l'export della radice importato in una collezione aggancia le radici del file lì;
  - l'import di un file con ciclo, ID duplicati o riferimento orfano lancia un'eccezione e lascia l'albero invariato.
- [X] T027 [P] [US4] Aggiungere a `tests/RemoteLogViewer.Tests/SessionStoreTests.cs`:
  - `ExportFile` → `ReadExport` round-trip;
  - `format` errato → `"File non riconosciuto."`;
  - `version` 2 → `"Versione del file non supportata."`;
  - JSON non valido → `"File non valido."`;
  - campi sconosciuti ignorati.

### Implementation (US4)

- [X] T028 [US4] In `src/RemoteLogViewer/SessionTree.cs` aggiungere:
  - `record ExportFile(string Format, int Version, List<Collection> Collections, List<SessionConfig> Sessions)` con `Format = "remote-log-viewer-collection"`, `Version = 1`;
  - `Export(Guid? collectionId)`: copie via `Clone()`, `ProtectedPassword = null`, radice con `ParentId = null`;
  - `Import(ExportFile file, Guid? targetParentId)`, che restituisce il primo elemento importato:
    - prima valida (ID unici, riferimenti esistenti, niente cicli) e lancia `InvalidDataException("File non valido.")`;
    - poi rimappa ogni ID a un nuovo `Guid`, aggancia le radici a `targetParentId` con `UniqueName` per le collezioni (e per le sessioni omonime alla stessa radice) e forza `ProtectedPassword = null`.
- [X] T029 [US4] In `src/RemoteLogViewer/SessionStore.cs` aggiungere:
  - `static void WriteExport(string path, ExportFile f)`, atomico come `Save`;
  - `static ExportFile ReadExport(string path)`, che lancia `InvalidDataException` con i messaggi del contract [export-file.md](contracts/export-file.md) (JsonException → `"File non valido."`).
- [X] T030 [US4] In `src/RemoteLogViewer/MainWindow.xaml.cs` aggiungere le voci "Esporta…" (collezione) / "Esporta tutto…" (spazio vuoto) e "Importa qui…" / "Importa…" al ContextMenu di T021:
  - dialog con filtro `Collezioni Remote Log Viewer (*.rlv.json)|*.rlv.json|JSON (*.json)|*.json` e nome proposto `<nome collezione>.rlv.json`;
  - import: `ReadExport` → `Import` → `Save()` → `RebuildTree()` con espansione e selezione del primo elemento importato;
  - errori mostrati in un `MessageBox` e albero invariato;
  - dopo l'import, messaggio informativo: `Importate N sessioni. Le password non sono incluse: inseriscile con Modifica.`
- [X] T031 [US4] Eseguire `dotnet test --filter "FullyQualifiedName~SessionTreeTests|FullyQualifiedName~SessionStoreTests"` e gli scenari 13–16 di quickstart.md

---

## Phase 7: Polish & Cross-Cutting

- [X] T032 [P] Aggiornare `README.md`: collezioni, export/import (senza password), riordino schede, segnaposto `{date:fmt}` con l'esempio `standard_logs\{date:yyyy_MM_dd}\log.log`, cambio giorno automatico solo per "oggi"
- [X] T033 [P] Aggiornare `CLAUDE.md` Architecture/Gotchas:
  - `SessionTree` piatto con `ParentId`, albero UI ricostruito;
  - `sessions.json` v2 retro-compatibile;
  - `ActiveSession` con `Date`/`FollowToday`/orologio iniettabile;
  - `FullPath` non risolto vs `ResolvePath`;
  - drag delle schede che sposta i `TabItem` (il `LogView` resta nel `Tag`).
- [X] T034 `dotnet build` e `dotnet test` completi; ripristinare `%APPDATA%\RemoteLogViewer\sessions.json` dal backup di T001; eliminare `C:\temp\rlv`

---

## Dependencies & Execution Order

- **Setup (T001)** → tutto.
- **US1 (T002–T011)** e **US2 (T012–T023)**: indipendenti come funzionalità, ma condividono `SessionConfig.cs` e `MainWindow.xaml(.cs)`, quindi vanno eseguite in sequenza (prima US1).
- **US3 (T024–T025)**: indipendente; tocca solo `Open()`/tab in `MainWindow.xaml.cs`, dopo T009 per evitare conflitti.
- **US4 (T026–T031)**: dipende da US2 (T014, T016, T021).
- **Polish (T032–T034)**: alla fine.

Dentro una storia: test [P] → logica pura → store → UI → validazione.

## Parallel Opportunities

- US1: T002, T003, T004, T005, T008 in parallelo (file diversi); poi T006 → T007 → T009 → T010.
- US2: T012, T013, T017 in parallelo; T014 → T015 → T016 → T018 → T019/T020/T021 → T022.
- US4: T026 e T027 in parallelo; poi T028 → T029 → T030.
- Polish: T032 ∥ T033.

## Implementation Strategy

1. **MVP = US1** (T001–T011): risolve il problema quotidiano del percorso giornaliero sulla lista piatta attuale.
2. **+ US2**: organizzazione in collezioni.
3. **+ US3**: piccolo e isolato, può anche andare prima di US2 se serve subito.
4. **+ US4**: condivisione di collezioni.
5. Polish e aggiornamento della documentazione (obbligatorio per CLAUDE.md).
