---
description: "Task list: apertura di un file log dal file system"
---

# Tasks: Apertura di un file log dal file system

**Input**: `specs/002-usability-improvements-open-local-file/` (plan.md, spec.md, research.md, data-model.md, contracts/ui.md, quickstart.md)

**Tests**: solo sulla logica non-UI (xUnit in `tests/RemoteLogViewer.Tests/`), come da plan. I test su file locale non usano SMB. La UI si valida con quickstart.md.

## Format: `[ID] [P?] [Story] Description`

Ogni stringa UI nuova va aggiunta alla tabella `En` di src/RemoteLogViewer/L.cs (chiave = testo italiano); in XAML `{local:T '…'}`.

## Phase 1: Setup

- [X] T001 Baseline: `dotnet build` e `dotnet test` verdi prima delle modifiche (repo root). Se `RemoteLogViewer.exe` blocca la build: `taskkill //IM RemoteLogViewer.exe //F`

## Phase 2: Foundational (blocca tutte le story)

- [X] T002 [P] Test in tests/RemoteLogViewer.Tests/SessionConfigTests.cs: `SessionConfig.Local(@"C:\log\app.log")` → `IsLocal == true`, `Name == "app.log"`, `FilePath == @"C:\log\app.log"`, `TailLines == 1000`, `Encoding == "auto"`, `FullPath == @"C:\log\app.log"` e `ResolvePath(DateTime.Today) == @"C:\log\app.log"` (nessun `Combine` con `SharePath`); un percorso relativo viene normalizzato con `Path.GetFullPath`; una config non locale si comporta come prima
- [X] T003 [P] Test in tests/RemoteLogViewer.Tests/ActiveSessionTests.cs (senza SMB, file in `Path.GetTempPath()`, da non saltare mai): `ActiveSession(SessionConfig.Local(file))` → ultime 3 righe con `TailLines = 3` (impostare la proprietà dopo `Local`), riga aggiunta con `File.AppendAllText` ricevuta entro 2 s, troncamento → marker + nuove righe, file cancellato → `SessionState.Waiting`, file ricreato → di nuovo `Running` con marker; `Stop()` → `Stopped`. Nessuna chiamata a `SmbConnection`
- [X] T004 In src/RemoteLogViewer/SessionConfig.cs: aggiungere `public bool IsLocal { get; set; }` e `public static SessionConfig Local(string path)` (`Name = Path.GetFileName(full)`, `FilePath = full` con `full = Path.GetFullPath(path)`, `IsLocal = true`; `TailLines` 1000 e `Encoding` "auto" restano i default); `FullPath` e `ResolvePath(...)` restituiscono `FilePath` così com'è se `IsLocal` (senza `Combine`)
- [X] T005 In src/RemoteLogViewer/ActiveSession.cs `Run`: se `Config.IsLocal` non chiamare `SmbConnection.Connect` (lasciare `connected = true`) né `SmbConnection.Release` (due punti: nel `catch` I/O e a fine ciclo); nessun altro cambiamento della macchina a stati. T002 e T003 devono passare

**Checkpoint**: `ActiveSession` segue un file locale senza rete.

---

## Phase 3: User Story 1 - Aprire un file con il dialogo di selezione (P1) 🎯 MVP

**Goal**: *Apri file…* / `Ctrl+O` apre uno o più file in nuove schede.

**Independent Test**: *Apri file…*, scegli un `.log`: scheda con il nome del file, ultime righe visibili, nuove righe in coda.

- [X] T006 [P] [US1] Stringhe `En` in src/RemoteLogViewer/L.cs: `Apri file…` → `Open file…`; `Apri un file di log (Ctrl+O)` → `Open a log file (Ctrl+O)`; `Apri file di log` → `Open log file`; `Log e testo (*.log;*.txt)|*.log;*.txt|Tutti i file (*.*)|*.*` → `Log and text (*.log;*.txt)|*.log;*.txt|All files (*.*)|*.*`; `Impossibile aprire «{0}»: {1}` → `Cannot open "{0}": {1}`; `Nessun file valido` → `No valid file`
- [X] T007 [US1] In src/RemoteLogViewer/MainWindow.xaml: `CommandBinding Command="ApplicationCommands.Open" Executed="Open_Executed"` nei `Window.CommandBindings` e, nella toolbar accanto a *Credenziali…*, `<Button Content="{local:T 'Apri file…'}" Command="ApplicationCommands.Open" ToolTip="{local:T 'Apri un file di log (Ctrl+O)'}" />` seguito da un `Separator`
- [X] T008 [US1] In src/RemoteLogViewer/MainWindow.xaml.cs: `Open_Executed` apre un `Microsoft.Win32.OpenFileDialog` (`Multiselect = true`, `Title = L.T("Apri file di log")`, `Filter = L.T("Log e testo (*.log;*.txt)|*.log;*.txt|Tutti i file (*.*)|*.*")`) e passa `dlg.FileNames` a un nuovo `private void OpenFiles(IEnumerable<string> paths)`. `OpenFiles`, per ogni percorso nell'ordine dato: (1) scarta ciò che non è `File.Exists` (cartelle incluse); se nessun elemento è utilizzabile mostra `L.T("Nessun file valido")` e ritorna; (2) se una scheda locale (`Tabs` `Tag` `LogView` con `Session.Config.IsLocal`) ha lo stesso `Path.GetFullPath`, confronto `StringComparison.OrdinalIgnoreCase`, la seleziona e passa al successivo; (3) prova `new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)` e su `IOException`/`UnauthorizedAccessException` mostra `L.F("Impossibile aprire «{0}»: {1}", p, ex.Message)` (MessageBox) senza aprire la scheda; (4) `var config = SessionConfig.Local(p)`; se un'altra scheda locale ha già `Config.Name == config.Name` allora `config.Name = Path.Combine(Path.GetFileName(Path.GetDirectoryName(config.FilePath)) ?? "", config.Name)`; (5) `Open(config, null)`. Alla fine l'ultima scheda aperta o trovata resta selezionata

**Checkpoint**: apertura da dialogo funzionante. MVP.

---

## Phase 4: User Story 3 - Stesse funzioni delle sessioni remote (P1)

**Goal**: una scheda locale si comporta come le remote (filtri, ricerca avanzata, *Precedenti*, salva, avvisi, Affianca, rotazione, attesa).

**Independent Test**: due file locali + una sessione remota: ricerca avanzata, filtri, *▲ Precedenti*, *Salva…*, badge su scheda non visibile.

- [X] T009 [US3] Verificare e correggere gli usi di `Config.SharePath`/`FullPath`/`FilePath`/`Name`/`CredentialId` fuori da `SessionConfig`/`ActiveSession` (`grep` in src/RemoteLogViewer/LogView.xaml.cs, MainWindow.xaml.cs, SearchWindow.xaml.cs, DateDialog.xaml.cs): nessuno deve assumere una share o un percorso relativo per una config locale. In particolare `LogView.SaveVisible` ricava il nome file da `Config.Name` (con `cartella\nome` il `\` è già sostituito da `_`: verificarlo) e `Open(SessionConfig)` con `DatePath.Has` non deve essere raggiunta da un file locale (si usa `Open(config, null)`)
- [ ] T010 [US3] Controllare con quickstart.md passi 2, 7, 8, 9 (tail, filtri/ricerca/*Precedenti*/*Salva…*/ricerca avanzata, badge e lampeggio, file cancellato e ricreato) e annotare/correggere eventuali differenze rispetto a una sessione remota

---

## Phase 5: User Story 2 - Trascinare un file sulla finestra (P2)

**Goal**: drag&drop di file da Esplora risorse apre le schede come col dialogo.

**Independent Test**: trascinare un `.log` sulla finestra (anche sopra una scheda o sull'elenco sessioni): si apre una scheda; i drag interni continuano a funzionare.

- [X] T011 [P] [US2] In src/RemoteLogViewer/MainWindow.xaml: sulla `Window` `AllowDrop="True" PreviewDragOver="Window_PreviewDragOver" PreviewDrop="Window_PreviewDrop"`
- [X] T012 [US2] In src/RemoteLogViewer/MainWindow.xaml.cs: `Window_PreviewDragOver`: solo se `e.Data.GetDataPresent(DataFormats.FileDrop)` → `e.Effects = DragDropEffects.Copy; e.Handled = true` (altrimenti non toccare l'evento: i drag interni usano i formati `rlv-tab` e `rlv-tree-item`). `Window_PreviewDrop`: solo se `FileDrop` presente → `OpenFiles((string[])e.Data.GetData(DataFormats.FileDrop))` e `e.Handled = true`. Verificare a mano che riordino schede e spostamento nell'elenco sessioni funzionino ancora (quickstart passo 6)

---

## Phase 6: User Story 4 - Riapertura all'avvio dei file locali (P3)

**Goal**: le schede locali tornano al riavvio se il file esiste ancora, altrimenti sono saltate in silenzio.

**Independent Test**: aprire un file locale e una sessione, chiudere e riavviare: entrambe tornano nello stesso ordine; con il file locale eliminato, resta solo la sessione.

- [X] T013 [P] [US4] Test in tests/RemoteLogViewer.Tests/SessionStoreTests.cs: roundtrip di un `Workspace` con `OpenTab { LocalPath = @"C:\x\app.log" }` (`SessionId = Guid.Empty`) e di una sessione normale; un `sessions.json` senza il campo `localPath` si legge con `LocalPath == null`
- [X] T014 [US4] In src/RemoteLogViewer/SessionTree.cs: `OpenTab` + `public string? LocalPath { get; set; } // local file tab: SessionId is empty`
- [X] T015 [US4] In src/RemoteLogViewer/MainWindow.xaml.cs: `Window_Closing` salva per ogni vista con `Session.Config.IsLocal` un `OpenTab { LocalPath = Session.Config.FilePath }` (le altre come prima); `Window_Loaded`, nel ciclo sulle schede, se `t.LocalPath` è valorizzato: `File.Exists` → `Open(SessionConfig.Local(t.LocalPath), null)` (con la stessa regola del titolo duplicato di T008, estratta in un metodo `LocalConfig(string path)` usato da entrambi) altrimenti `continue` in silenzio, **prima** del controllo sessione eliminata; l'indice `Selected` resta riferito all'ordine delle schede effettivamente riaperte (come oggi per le sessioni saltate)

---

## Phase 7: Polish & cross-cutting

- [X] T016 [P] Aggiornare src/RemoteLogViewer/Guida.it.html e Guida.en.html: nuova sezione (stesso id in entrambe, es. `file-locali`, + voce nell'indice) su apertura da dialogo/trascinamento, schede su file locale (nessuna credenziale, 1000 righe iniziali, nessuna sessione salvata, riapertura all'avvio), riga `Ctrl+O` nella tabella delle scorciatoie; `GuideTests` verdi
- [X] T017 [P] Aggiornare README.md e README.it.md (funzione *Apri file…*/`Ctrl+O`/trascinamento) e CLAUDE.md (architettura: `SessionConfig.IsLocal`/`Local`, `ActiveSession` senza SMB per i locali, `MainWindow.OpenFiles`/`LocalConfig`, `PreviewDragOver`/`PreviewDrop` solo per `FileDrop`, `OpenTab.LocalPath`; aggiornare anche la frase iniziale "tail log files on remote SMB shares")
- [X] T018 `dotnet build` e `dotnet test` (suite completa, incl. `LocTests` per le stringhe `En`); correggere ogni errore
- [ ] T019 Prova manuale dei passi 1-12 di quickstart.md (`taskkill //IM RemoteLogViewer.exe //F` prima; ripulire i file temporanei e il workspace dopo)

---

## Dependencies

- Phase 1 → Phase 2 → US1 (MVP).
- US3 dipende da US1 (serve poter aprire il file) e da Phase 2; US2 e US4 dipendono da US1 (`OpenFiles`, `LocalConfig`); US2, US3, US4 sono indipendenti tra loro (MainWindow.xaml.cs condiviso: coordinare le modifiche).
- Dentro le story: test → implementazione → UI. T004/T005 dopo T002/T003. T015 dopo T008 e T014.
- Polish dopo le story volute.

## Parallel examples

- Phase 2: T002 e T003 in parallelo (file diversi), poi T004 e T005 in sequenza.
- US1: T006 in parallelo a T007; T008 dopo.
- US4: T013 in parallelo a T014.
- Polish: T016 e T017 in parallelo.

## Implementation strategy

1. **MVP**: Phase 1-3 (US1): *Apri file…* con dialogo e schede su file locale.
2. US3 (parità, per lo più verifica), poi US2 (drag&drop), poi US4 (riapertura).
3. Polish a fine lavoro: guide, README, `CLAUDE.md`, suite completa, prova manuale.
