---
description: "Task list: miglioramenti di usabilità"
---

# Tasks: Miglioramenti di usabilità (workspace, avvisi, contesto, guida)

**Input**: `specs/002-usability-improvements/` (plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md)

**Tests**: solo sulla logica non-UI, come previsto dal plan (xUnit in `tests/RemoteLogViewer.Tests/`). La UI si valida con quickstart.md.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Setup

- [X] T001 Verificare la baseline: `dotnet build` e `dotnet test` verdi prima delle modifiche (repo root)

## Phase 2: Foundational (blocca US1 e US6)

- [X] T002 Estendere `LogLine` in src/RemoteLogViewer/LogLevel.cs: parametri del costruttore `isEntry` e `isHistory` (proprietà solo get `IsEntry`, `IsHistory`) e proprietà `IsContext` con `PropertyChanged`, sullo stesso modello di `IsMatch`. Resta una class, non un record
- [X] T003 In src/RemoteLogViewer/ActiveSession.cs `Emit(lines, history)`: `own = LogLevels.Detect(l, LogLevel.None)`; se `own != None` allora `_last = own`; accodare `new LogLine(l, _last, isEntry: own != None, isHistory: history)`. Passare `history: true` alla `ReadTail` iniziale e alla ri-lettura dopo "file scomparso", `false` al `ReadTail` dall'inizio dopo il cambio giorno e a `Poll`
- [X] T004 [P] Test in tests/RemoteLogViewer.Tests/LogLevelTests.cs: una riga `ERROR` seguita da righe `   at …` ha `IsEntry` solo sulla prima (tramite il metodo helper usato da `Emit`; se serve estrarre `static LogLine Make(string, ref LogLevel, bool)` in `LogLevels`)

**Checkpoint**: le righe portano i flag voce/storico.

---

## Phase 3: User Story 1 - Avviso di errori nelle sessioni non visibili (P1) 🎯 MVP

**Goal**: badge con il numero di ERROR sulle schede non visibili, lampeggio della taskbar se l'app non è attiva.

**Independent Test**: quickstart scenari 2 e 3.

- [X] T005 [US1] In src/RemoteLogViewer/LogView.xaml.cs: proprietà `UnreadErrors`; evento `ErrorsArrived(int)`; in `Drain` contare le righe con `Level == Error && IsEntry && !IsHistory`; se > 0 sollevare `ErrorsArrived`, e se `!IsVisible` fare `UnreadErrors += n` e sollevare `StatusChanged`. `IsVisibleChanged` → se visibile, `UnreadErrors = 0` e `StatusChanged`
- [X] T006 [US1] In src/RemoteLogViewer/MainWindow.xaml.cs `Open`: `TextBlock` badge rosso (sfondo `#D32F2F`, testo bianco, collassato se 0) tra titolo e ✕, aggiornato nell'handler di `view.StatusChanged`
- [X] T007 [US1] In src/RemoteLogViewer/MainWindow.xaml.cs: P/Invoke `FlashWindowEx` (`FLASHWINFO`, `FLASHW_ALL | FLASHW_TIMERNOFG`, handle da `WindowInteropHelper`); su `view.ErrorsArrived` chiamarla se `!IsActive`

**Checkpoint**: US1 verificabile a mano.

---

## Phase 4: User Story 2 - Ripristino delle sessioni aperte (P1)

**Goal**: all'avvio riapre schede, ordine, selezione e Affianca.

**Independent Test**: quickstart scenario 6.

- [X] T008 [US2] In src/RemoteLogViewer/SessionTree.cs: classi `Workspace { List<OpenTab> Tabs = []; int Selected = -1; bool SideBySide }` e `OpenTab { Guid SessionId; DateTime? Date; bool FollowToday }`; proprietà `public Workspace Workspace { get; set; } = new();` su `SessionTree`
- [X] T009 [US2] In src/RemoteLogViewer/SessionStore.cs: `SessionsFile` con `Workspace? Workspace`; `Load` assegna `tree.Workspace = f.Workspace ?? new()`; `Save` lo scrive. L'export non cambia
- [X] T010 [P] [US2] Test in tests/RemoteLogViewer.Tests/SessionStoreTests.cs: round-trip del workspace (2 tab, date null/valorizzata, followToday, selected, sideBySide); un file v1 senza `workspace` dà un workspace vuoto; l'export non contiene `workspace`
- [X] T011 [US2] In src/RemoteLogViewer/MainWindow.xaml.cs: separare `Open(SessionConfig)` (dialog della data) da `Open(SessionConfig, DateTime?)` (crea la scheda)
- [X] T012 [US2] In src/RemoteLogViewer/MainWindow.xaml.cs `Window_Closing`: costruire il `Workspace` dalle schede (`view.Session.Config.Id`, `Date`, `FollowToday`), dall'indice selezionato e da `SideBySideButton`; `_tree.Workspace = …`; `Save()` in try/catch IO (una chiusura non deve mai fallire)
- [X] T013 [US2] In src/RemoteLogViewer/MainWindow.xaml.cs, su `Loaded`: per ogni `OpenTab` cercare la sessione (se manca, saltarla), calcolare la data (`!DatePath.Has` → null; `FollowToday` → oggi; altrimenti `Date ?? oggi`) e chiamare `Open(config, date)`; poi `SideBySideButton.IsChecked` e `Tabs.SelectedIndex` (se nel range)

**Checkpoint**: US2 verificabile a mano.

---

## Phase 5: User Story 3 - Righe di contesto (P2)

**Goal**: grep -C N sul filtro testo/regex.

**Independent Test**: quickstart scenario 4.

- [X] T014 [US3] Creare src/RemoteLogViewer/ContextFilter.cs: `ContextFilter(int context)` con `Add(IReadOnlyList<LogLine> all, int i, Func<LogLine,bool> levelOk, Func<string,bool>? text)` che restituisce le righe da aggiungere alla vista (algoritmo in research.md R7). Separatore = `new LogLine("--", LogLevel.None, isMarker: true)`. Imposta `IsContext` sulle righe emesse. Con `context == 0` o `text == null` restituisce il predicato attuale: `IsMarker || (levelOk && (text == null || text(l.Text)))`
- [X] T015 [P] [US3] Test in tests/RemoteLogViewer.Tests/ContextFilterTests.cs: 100 righe con match alla 50 e N=2 → 48–52; match a 50 e 53 → un solo gruppo 48–55; match lontani → separatore `--`; N=0 identico al predicato; righe con livello nascosto escluse ma contate come posizioni; marker sempre emessi; righe di contesto successive che arrivano una per volta (incrementale)
- [X] T016 [US3] In src/RemoteLogViewer/LogView.xaml: `TextBlock "Contesto:"` + `TextBox x:Name="ContextBox"` (larghezza 30, testo "0", tooltip "Righe prima/dopo ogni riga filtrata (0–50)") accanto a Regex; nello stile degli item, `DataTrigger IsContext=True → Opacity 0.55`
- [X] T017 [US3] In src/RemoteLogViewer/LogView.xaml.cs: il campo `_ctx` (`ContextFilter`) viene ricreato in `Rebuild` con N letto e limitato a 0–50 da `ContextBox`; `Rebuild` = reset + `Add` su ogni indice; `Drain` usa `Add` sulla nuova riga; `ContextBox.TextChanged` → `Rebuild` (guard `IsLoaded`). Rimuovere `Matches` se non serve più

**Checkpoint**: US3 verificabile.

---

## Phase 6: User Story 4 - Carica righe precedenti (P2)

**Goal**: pulsante che aggiunge in testa il blocco di righe precedente.

**Independent Test**: quickstart scenario 1.

- [X] T018 [US4] Riscrivere `ReadTail` in src/RemoteLogViewer/LogTailer.cs con la scansione dei byte all'indietro (research.md R5): i campi `_start` (preambolo) e `_head`; `FindBack(fs, from, count)` a blocchi da 256 KB, allineato a 2 byte per UTF-16 (LE `0A 00`, BE `00 0A`, codepage 1201); `tailEnd = FindBack(len, 1)`; `_head = n == 0 || tailEnd == _start ? tailEnd : FindBack(tailEnd - unit, n)`; restituisce le righe decodificate da `[_head, tailEnd)`; `_partial` = decodifica di `[tailEnd, len)`. Comportamento esterno invariato (i test esistenti restano verdi)
- [X] T019 [US4] In src/RemoteLogViewer/LogTailer.cs: `public bool HasOlder => _head > _start`; `public List<string> ReadBefore(int n)`, che restituisce `[]` se `!HasOlder`, altrimenti `newHead = FindBack(_head - unit, n)`, decodifica `[newHead, _head)` e poi `_head = newHead`. In `Poll`, alla rotazione, `_start = DetectEncoding(fs)` e `_head = _start`
- [X] T020 [P] [US4] Test in tests/RemoteLogViewer.Tests/LogTailerTests.cs: file da 5000 righe, `ReadTail(1000)` + `ReadBefore(1000)` → righe 3001–4000 contigue; fino all'inizio `HasOlder` diventa false e `ReadBefore` restituisce `[]`; CRLF; UTF-16 LE con BOM; caratteri multibyte UTF-8 (`è`, emoji) sui confini dei blocchi da 256 KB; rotazione → `HasOlder == false`; `ReadTail(0)` → nessuna riga e `HasOlder == true`
- [X] T021 [US4] In src/RemoteLogViewer/ActiveSession.cs: `RequestOlder(int n)` (`Interlocked.Exchange`), `ConcurrentQueue<List<LogLine>> Older`, `CanLoadOlder` (volatile). Nel loop, nel ramo `tailer != null` prima di `Poll`: se c'è una richiesta, `ReadBefore(k)` → righe con livello rilevato localmente (prev = None), `isHistory: true`, accodate in `Older`. Dopo ogni iterazione ricalcolare `CanLoadOlder = State == Running && tailer?.HasOlder == true`; se cambia, `StateChanged`. Fuori dal loop / in errore, `CanLoadOlder = false`
- [X] T022 [US4] In src/RemoteLogViewer/LogView.xaml: pulsante `x:Name="OlderButton" Content="▲ Precedenti"` con tooltip "Carica righe precedenti a quelle lette"
- [X] T023 [US4] In src/RemoteLogViewer/LogView.xaml.cs: il click calcola `n = TailLines > 0 ? TailLines : 1000`, limitato a `MaxLines - _all.Count`; se 0 → messaggio "Buffer pieno (100.000 righe)…"; se ridotto → messaggio con le righe effettive; poi `Session.RequestOlder(n)`. In `Drain`, svuotare prima `Session.Older`: `_all.InsertRange(0, block)`, aggiornare i contatori, poi `Rebuild` preservando lo scroll (research.md R8: se `!Follow`, `ScrollToVerticalOffset(old + delta)` a `DispatcherPriority.Loaded`). `UpdateStatus` imposta `OlderButton.IsEnabled = Session.CanLoadOlder`

**Checkpoint**: US4 verificabile.

---

## Phase 7: User Story 7 - Guida utente (P2)

**Goal**: guida HTML in italiano apribile con F1 / pulsante Guida.

**Independent Test**: quickstart scenario 7.

- [X] T024 [P] [US7] Creare src/RemoteLogViewer/Guida.html: HTML autonomo in italiano, CSS inline, nessuna risorsa esterna, versione `%VERSION%` nell'intestazione, indice con ancore, sezioni: introduzione, sessioni (campi), credenziali, collezioni + drag & drop + export/import, data nel percorso, schede/Affianca/riordino, ripristino workspace, avvisi errori, barra della sessione (Follow, A capo, Pulisci, filtro/regex, contesto, livelli con contatori, ricerca, precedenti, salva), stati della sessione, scorciatoie (tabella completa da contracts/ui.md), problemi comuni (errore 1219, password dopo import, credenziali non decifrabili). Tema chiaro/scuro con `prefers-color-scheme`
- [X] T025 [US7] In src/RemoteLogViewer/RemoteLogViewer.csproj: `<EmbeddedResource Include="Guida.html" LogicalName="RemoteLogViewer.Guida.html" />`
- [X] T026 [US7] In src/RemoteLogViewer/MainWindow.xaml(.cs): pulsante `Guida` nella toolbar (tooltip "Guida utente (F1)"), `KeyBinding F1 → ApplicationCommands.Help` sulla finestra; l'handler legge la risorsa, sostituisce `%VERSION%` con la versione dell'assembly (`InformationalVersion` senza `+hash`), scrive in `%TEMP%\RemoteLogViewer\guida.html` e fa `Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })`; in caso di errore, MessageBox con il percorso

---

## Phase 8: User Story 5 - Salvare le righe visibili (P3)

**Independent Test**: quickstart scenario 5.

- [X] T027 [US5] In src/RemoteLogViewer/LogView.xaml(.cs): `KeyBinding Ctrl+S → ApplicationCommands.Save` + pulsante `Salva…`; l'handler non fa nulla se `_visible.Count == 0`; `SaveFileDialog` con filtro `Log (*.log)|*.log|Tutti i file (*.*)|*.*` e nome `<Config.Name con caratteri invalidi → _>_{DateTime.Now:yyyyMMdd_HHmmss}.log`; `File.WriteAllLines(path, _visible.Select(l => l.Text), new UTF8Encoding(false))`; errori IO/permessi → MessageBox

---

## Phase 9: User Story 6 - Contatori per livello (P3)

**Independent Test**: quickstart scenario 1 (contatori) e 8 (*Pulisci*).

- [X] T028 [US6] In src/RemoteLogViewer/LogView.xaml: `x:Name` sulle 4 checkbox di livello (`ErrorCheck`, `WarnCheck`, `InfoCheck`, `DebugCheck`)
- [X] T029 [US6] In src/RemoteLogViewer/LogView.xaml.cs: `int[] _counts` indicizzato per `LogLevel`; `+1` per ogni riga `IsEntry` in `Drain`/prepend; ricalcolo completo dopo il trim; `Clear_Click` azzera; `UpdateCounts()` imposta `Content = $"ERROR {n}"` ecc. (chiamato in `UpdateCount`)

---

## Phase 10: Polish

- [X] T030 Aggiornare README.md (sezione Funzioni: ripristino, avvisi, contatori, contesto, precedenti, salva, guida F1)
- [X] T031 Aggiornare CLAUDE.md: architettura (flag di `LogLine`, `ContextFilter`, `_head`/`ReadBefore`, `Older`/`RequestOlder`, workspace, guida embedded); Workflow → "dopo ogni modifica aggiornare anche `src/RemoteLogViewer/Guida.html` se cambia qualcosa visibile all'utente (funzioni, comandi, scorciatoie, messaggi)"
- [X] T032 `dotnet build` + `dotnet test` (fuori dalla sandbox per i test SMB) e validazione a mano degli scenari di quickstart.md; ripristinare `sessions.json`

---

## Dependencies & Execution Order

- Setup → Foundational (T002–T004) → US1 e US6 (usano `IsEntry`/`IsHistory`).
- US2, US3, US4, US5, US7 non dipendono dalla Foundational, tranne US4, che usa `isHistory` di T002.
- Stesso file = sequenziale: `LogView.xaml.cs` (T005, T017, T023, T027, T029), `MainWindow.xaml.cs` (T006, T007, T011–T013, T026), `LogTailer.cs` (T018 → T019), `ActiveSession.cs` (T003 → T021).
- Polish per ultima.

## Parallel Opportunities

- I test [P] (T004, T010, T015, T020) e la guida (T024) sono su file separati.
- US3 (`ContextFilter.cs` + test) e US4 (`LogTailer.cs` + test) si possono sviluppare in parallelo fino all'integrazione in `LogView`.

## Implementation Strategy

1. MVP: Foundational + US1 (avvisi) + US2 (workspace).
2. Poi US4 e US3, i più delicati (tailer, filtro), con i loro test.
3. Poi US7, US5, US6 (piccoli), infine Polish con la guida allineata a tutto.
