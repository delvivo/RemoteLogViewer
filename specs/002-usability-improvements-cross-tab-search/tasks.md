---
description: "Task list: ricerca avanzata tra tutti i tab aperti"
---

# Tasks: Ricerca avanzata tra tutti i tab aperti

**Input**: `specs/002-usability-improvements-cross-tab-search/` (plan.md, spec.md, research.md, data-model.md, contracts/ui.md, quickstart.md)

**Tests**: solo sulla logica pura (xUnit in `tests/RemoteLogViewer.Tests/`), come da plan. La UI si valida con quickstart.md.

## Format: `[ID] [P?] [Story] Description`

Ogni stringa UI nuova va aggiunta alla tabella `En` di src/RemoteLogViewer/L.cs (chiave = testo italiano); in XAML `{local:T '…'}`.

## Phase 1: Setup

- [X] T001 Baseline: `taskkill //IM RemoteLogViewer.exe //F` se serve, poi `dotnet build` e `dotnet test` verdi prima delle modifiche (repo root)

## Phase 2: Foundational (blocca tutte le story)

- [X] T002 Creare src/RemoteLogViewer/LogSearch.cs con i tipi di data-model.md: `record SearchCriteria(string Text, bool IsRegex, bool MatchCase, bool WholeWord, IReadOnlySet<LogLevel>? Levels, DateTime? From, DateTime? To)` con `IsEmpty` (testo vuoto, `Levels` null o con tutti e 4 i livelli, `From`/`To` null); `record SearchSource(string Name, IReadOnlyList<LogLine> Lines, DateTime DefaultDate)`; `record SearchHit(int Source, LogLine Line, int Start, int Length)`; `record SearchResult(IReadOnlyList<SearchHit> Hits, bool Truncated)`; `static class LogSearch` con `public const int MaxHits = 10_000` e `Run(IReadOnlyList<SearchSource>, SearchCriteria, CancellationToken)` (corpo `throw new NotImplementedException()` per ora)
- [X] T003 [P] In src/RemoteLogViewer/LogView.xaml.cs aggiungere `public LogLine[] Snapshot() => _all.ToArray();` (da chiamare sul thread UI)

**Checkpoint**: tipi e istantanea disponibili.

---

## Phase 3: User Story 1 - Cercare un testo in tutte le sessioni aperte (P1) 🎯 MVP

**Goal**: testo/regex nei buffer di tutte le sessioni, risultati raggruppati per scheda.

**Independent Test**: due sessioni con la stessa parola → `Ctrl+Maiusc+F`, cerca: due gruppi con conteggi.

- [X] T004 [P] [US1] Test in tests/RemoteLogViewer.Tests/LogSearchTests.cs: testo semplice (insensibile alle maiuscole di default), regex valida, regex non valida → `ArgumentException` dal `Run` (nessun risultato parziale), nessuna corrispondenza → `Hits` vuoto, gruppi in ordine di sessione e di buffer, sessione senza corrispondenze assente, più righe corrispondenti nello stesso stack trace = un `SearchHit` ciascuna (FR-008b), testo vuoto e nessun filtro (`IsEmpty`) → nessun risultato, limite `MaxHits`: con 10.001 corrispondenze `Hits.Count == 10_000` e `Truncated == true`, `CancellationToken` annullato → `OperationCanceledException`
- [X] T005 [US1] Implementare `LogSearch.Run` in src/RemoteLogViewer/LogSearch.cs: un solo percorso `Regex` (testo semplice → `Regex.Escape`; `RegexOptions.IgnoreCase` di default; `RegexOptions.Compiled`; timeout 50 ms per riga, `RegexMatchTimeoutException` = nessuna corrispondenza); salta le righe marker (`IsMarker`); `Start`/`Length` = prima corrispondenza (0,0 con testo vuoto); `ct.ThrowIfCancellationRequested()` ogni ~1000 righe; si ferma a `MaxHits` con `Truncated = true`
- [X] T006 [US1] Creare src/RemoteLogViewer/SearchWindow.xaml e .xaml.cs: finestra non modale e ridimensionabile; costruttore `(Func<IReadOnlyList<LogView>> views, Action<LogView, LogLine> reveal)`; controlli testo (Invio = Cerca), checkbox Regex, pulsanti Cerca/Annulla (Annulla attivo solo durante la ricerca), etichetta di stato. Cerca: prende `Snapshot()` di ogni vista sul thread UI, costruisce i `SearchSource` (`Name` = `Session.DisplayName`, `DefaultDate` = `Session.Date ?? DateTime.Today`), lancia `Task.Run(() => LogSearch.Run(...))` con un `CancellationTokenSource` (una nuova ricerca annulla la precedente). Risultati in una `ListBox` virtualizzata su lista piatta di `ResultRow` (intestazione `Nome · N` non selezionabile oppure riga con livello e `Pre/Match/Post` evidenziato in un `TextBlock` con `Run`). Stati: "N risultati in M sessioni", "Nessun risultato", "Nessuna sessione aperta", "Risultati limitati a 10.000: restringi la ricerca", errore regex (bordo rosso + messaggio, risultati precedenti intatti). Testo che dipende dalla lingua con `L.Bind`/`L.Changed` e unsubscribe in `Closed`
- [X] T007 [US1] In src/RemoteLogViewer/MainWindow.xaml aggiungere il pulsante toolbar `{local:T 'Cerca…'}` (tooltip con `Ctrl+Maiusc+F`) e un `RoutedCommand`/`KeyBinding` `Ctrl+Shift+F`; in MainWindow.xaml.cs un `SearchWindow? _search` singleton: se esiste e non è chiuso → `Activate()`, altrimenti `new SearchWindow(() => Tabs.Items.Cast<TabItem>().Select(t => (LogView)t.Tag).ToList(), (v, l) => { /* US2 */ }) { Owner = this }.Show()`; chiuderla nella chiusura della finestra principale
- [X] T008 [P] [US1] Aggiungere a src/RemoteLogViewer/L.cs `En` tutte le stringhe introdotte da T006/T007

**Checkpoint**: la ricerca testo/regex funziona e mostra i gruppi. MVP.

---

## Phase 4: User Story 2 - Saltare alla riga trovata (P1)

**Goal**: doppio click → scheda giusta, riga in vista ed evidenziata.

**Independent Test**: doppio click su un risultato di una scheda non visibile con filtri attivi → scheda selezionata, filtri azzerati con avviso, riga evidenziata.

- [X] T009 [US2] In src/RemoteLogViewer/LogView.xaml.cs aggiungere `public RevealResult Reveal(LogLine line)` (enum `NotFound, Shown, ShownFiltersCleared`): se `!_all.Contains(line)` → `NotFound`; se la riga non è in `_visible` (nascosta da livelli o testo) → spuntare i quattro checkbox livello (Checked → `_hiddenLevels` svuotato), `FilterBox.Clear()`, `Rebuild()`; poi `FollowButton.IsChecked = false`, `Lines.SelectedItem = line`, `Lines.ScrollIntoView(line)`. `ShownFiltersCleared` solo se è stato necessario azzerare
- [X] T010 [US2] In src/RemoteLogViewer/MainWindow.xaml.cs implementare il delegato di T007: se non è attivo `SideBySideButton`, `Tabs.SelectedItem` = `TabItem` con `Tag == view`; poi `view.Reveal(line)`; restituire il risultato alla finestra di ricerca (cambiare il delegato in `Func<LogView, LogLine, RevealResult>`). Con `NotFound` non selezionare la scheda
- [X] T011 [US2] In src/RemoteLogViewer/SearchWindow.xaml.cs: doppio click o Invio su una riga risultato (non sulle intestazioni) chiama il delegato; `NotFound` → messaggio "La riga non è più disponibile nel buffer"; `ShownFiltersCleared` → avviso non modale "Filtri rimossi" nell'etichetta di stato della finestra di ricerca; per le righe mantenere un riferimento al `LogView` della sessione (aggiungerlo a `SearchSource`/`ResultRow` dalla lista `views`)
- [X] T012 [P] [US2] Stringhe nuove di T009-T011 in src/RemoteLogViewer/L.cs `En`

**Checkpoint**: US1 + US2 usabili insieme.

---

## Phase 5: User Story 3 - Opzioni di corrispondenza e filtri per livello e orario (P2)

**Goal**: maiuscole/minuscole, parola intera, livelli, intervallo data+ora.

**Independent Test**: `Timeout` con maiuscole/minuscole non trova `timeout`; intervallo orario esclude le righe fuori range.

- [X] T013 [P] [US3] Test in tests/RemoteLogViewer.Tests/LogTimestampTests.cs: `2026-10-01 12:34:56,789`, `2026-10-01T12:34:56.789Z` (suffisso ignorato), `[2026-10-01 12:34:56]`, `01/10/2026 12:34:56`, solo `12:34:56` e `12:34:56.789` con `defaultDate`, riga senza timestamp → `false`, timestamp non a inizio riga → `false`, data impossibile (`2026-13-40 …`) → `false`
- [X] T014 [P] [US3] Creare src/RemoteLogViewer/LogTimestamp.cs: `static bool TryParse(string line, DateTime defaultDate, out DateTime value)` con una regex compilata ancorata a inizio riga (spazi e `[` opzionali) per i formati di research.md §3; nessuna conversione di fuso
- [X] T015 [P] [US3] Aggiungere a tests/RemoteLogViewer.Tests/LogSearchTests.cs: `MatchCase` (`Timeout` vs `timeout`); `WholeWord` (`err` non trova `error`, trova `err`; `[ERR]` con testo `[ERR]` trova); livelli (solo `Error`; righe di stack trace ereditate incluse; righe `LogLevel.None` escluse quando il filtro è parziale, incluse quando sono selezionati tutti e 4); intervallo `From`/`To` con estremi inclusi; riga senza timestamp eredita l'ultimo della stessa sessione (e non quello di un'altra sessione); riga senza timestamp e nessun precedente → esclusa con intervallo attivo; riga con solo orario usa `DefaultDate`; testo vuoto + solo filtri → elenca per filtri
- [X] T016 [US3] Estendere `LogSearch.Run` in src/RemoteLogViewer/LogSearch.cs: `RegexOptions.IgnoreCase` solo se `!MatchCase`; con `WholeWord` il pattern diventa `(?<!\w)(?:…)(?!\w)`; filtro livelli (`Levels` null o con tutti e 4 = nessun filtro; altrimenti `line.Level` deve essere nel set, quindi `None` escluso); filtro orario: per sessione tenere `lastTimestamp` aggiornato a ogni riga con `LogTimestamp.TryParse`, usarlo per le righe senza timestamp; senza testo ma con filtri attivi nessun filtro testo (tutte le righe che passano). Dipende da T014
- [X] T017 [US3] In src/RemoteLogViewer/SearchWindow.xaml(.cs): checkbox Maiuscole/minuscole e Parola intera, quattro checkbox livello (default tutti, `Checked`/`Unchecked` con guardia `IsLoaded`), campi Da/A (`DatePicker` + `TextBox` ora `HH:mm[:ss]`, facoltativi; ora non valida → bordo rosso e ricerca bloccata con messaggio), costruzione di `SearchCriteria` completa; con `IsEmpty` mostrare "Inserisci un testo o un filtro" invece di cercare
- [X] T018 [P] [US3] Stringhe nuove di T017 in src/RemoteLogViewer/L.cs `En`

**Checkpoint**: tutti i filtri funzionano.

---

## Phase 6: User Story 4 - Esportare i risultati (P3)

**Goal**: salvare su file i risultati mostrati.

**Independent Test**: dopo una ricerca, `Esporta…` → il file ha le stesse righe, raggruppate con intestazione.

- [X] T019 [P] [US4] Test in tests/RemoteLogViewer.Tests/LogSearchTests.cs per `LogSearch.FormatExport`: due gruppi → `# Nome (N)` seguito dalle righe, riga vuota tra i gruppi, ordine come in lista, nessun risultato → stringa vuota
- [X] T020 [US4] Aggiungere `LogSearch.FormatExport(IReadOnlyList<SearchSource>, SearchResult)` in src/RemoteLogViewer/LogSearch.cs (formato di contracts/ui.md)
- [X] T021 [US4] In src/RemoteLogViewer/SearchWindow.xaml(.cs): pulsante `Esporta…` disabilitato senza risultati; `SaveFileDialog` (`.log`/`.txt`, nome `ricerca_yyyyMMdd_HHmmss.log`), `File.WriteAllText(..., new UTF8Encoding(false))`; `IOException`/`UnauthorizedAccessException` → messaggio di errore, risultati intatti. Esporta l'ultimo risultato mostrato (non rilancia la ricerca)
- [X] T022 [P] [US4] Stringhe nuove di T021 in src/RemoteLogViewer/L.cs `En`

---

## Phase 7: Polish & cross-cutting

- [X] T023 [P] Aggiornare src/RemoteLogViewer/Guida.it.html e Guida.en.html: nuova sezione sulla ricerca avanzata (stessi id sezione), riga `Ctrl+Maiusc+F` nella tabella delle scorciatoie; `GuideTests` verdi
- [X] T024 [P] Aggiornare README.md e README.it.md (feature e scorciatoia)
- [X] T025 [P] Aggiornare CLAUDE.md: `LogSearch`/`LogTimestamp` (pure, testate), `SearchWindow` singleton non modale e istantanea dei buffer, `LogView.Snapshot`/`Reveal` nella sezione `LogView`/`MainWindow`
- [X] T026 `dotnet build` e `dotnet test` (suite completa, incl. `LocTests` per le stringhe `En`); correggere ogni errore
- [ ] T027 Prova manuale dei passi 1-9 di quickstart.md (`taskkill //IM RemoteLogViewer.exe //F` prima; ripulire `sessions.json` dopo)

---

## Dependencies

- Phase 1 → Phase 2 → US1 (MVP).
- US2 dipende da US1 (finestra e lista risultati). US3 dipende da US1 (stessa `Run` e finestra); US3 e US2 indipendenti tra loro (tranne i file condivisi: coordinare le modifiche a SearchWindow). US4 dipende da US1.
- Dentro ogni story: test → implementazione → UI → stringhe. T016 dipende da T014.
- Polish dopo le story volute.

## Parallel examples

- Phase 2: T003 in parallelo a T002 (file diversi).
- US1: T004 e T008 in parallelo (T004 prima di T005).
- US3: T013, T014, T015 in parallelo (3 file diversi); poi T016.
- Polish: T023, T024, T025 in parallelo.

## Implementation strategy

1. **MVP**: Phase 1-3 (US1): ricerca testo/regex tra le sessioni con risultati raggruppati.
2. Aggiungere US2 (salto), valida a sé: ricerca utilizzabile davvero.
3. US3 (filtri), poi US4 (export).
4. Polish solo a fine lavoro (guide, README, CLAUDE.md, test completi).
