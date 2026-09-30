# Implementation Plan: Miglioramenti di usabilità (workspace, avvisi, contesto, guida)

**Branch**: `feature/002-usability-improvements` | **Date**: 2026-09-30 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/002-usability-improvements/spec.md`

## Summary

Sette estensioni all'app esistente, tutte senza nuove dipendenze:

1. **Avvisi** (US1): `LogView` conta le voci ERROR nuove mentre non è visibile (`IsVisible == false`, cioè scheda non selezionata) e le azzera quando torna visibile; `MainWindow` mostra il badge nel titolo della scheda e chiama `FlashWindowEx` se la finestra non è attiva.
2. **Workspace** (US2): `sessions.json` guadagna un campo `workspace` (schede aperte, selezionata, Affianca), scritto alla chiusura e riletto su `Loaded`.
3. **Contesto** (US3): nuova classe pura `ContextFilter` (grep -C N incrementale, riga per riga) usata sia da `Drain` che da `Rebuild`: un solo percorso di codice.
4. **Carica precedenti** (US4): `LogTailer` passa da "decodifica un blocco e taglia le stringhe" a una scansione all'indietro sui **byte** di fine riga, così conosce l'offset esatto della prima riga caricata (`_head`) e può leggere il blocco prima (`ReadBefore`). La richiesta dalla UI passa da `ActiveSession` (thread del loop) tramite un contatore atomico e una coda separata `Older`.
5. **Salva** (US5): `ApplicationCommands.Save` (Ctrl+S) + pulsante in `LogView`, `SaveFileDialog`, `File.WriteAllLines` UTF-8.
6. **Contatori** (US6): `LogLine.IsEntry` (livello rilevato sulla riga stessa, non ereditato); `LogView` tiene un array di conteggi aggiornato in modo incrementale; mostrati nelle etichette delle checkbox di livello (`ERROR 12`).
7. **Guida** (US7): `Guida.html` come `EmbeddedResource`; a F1 / pulsante *Guida* viene scritta in `%TEMP%\RemoteLogViewer\guida.html` con la versione dell'assembly sostituita e aperta nel browser predefinito.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0-windows`)

**Primary Dependencies**: WPF in-box, `System.Text.Json`, P/Invoke `user32!FlashWindowEx`. Nessuna nuova dipendenza.

**Storage**: `%APPDATA%\RemoteLogViewer\sessions.json` esteso con `workspace` (opzionale, retro-compatibile). Guida embedded nell'exe.

**Testing**: xUnit. Logica pura testata: `LogTailer.ReadTail`/`ReadBefore` (contiguità, CRLF, UTF-16, multibyte, rotazione), `ContextFilter`, `LogLine.IsEntry` via `ActiveSession`/`LogLevels`, round-trip `workspace` nello store. UI (badge, lampeggio, scroll, F1, Ctrl+S) validata con [quickstart.md](quickstart.md).

**Target Platform**: Windows 10/11 x64

**Project Type**: desktop-app

**Performance Goals**: badge entro 1 tick di drain (200 ms) dall'accodamento; `ReadBefore(1000)` = poche letture da 256 KB; salvataggio 100k righe < 3 s.

**Constraints**: nessuna modifica al comportamento di tail/rotazione/riconnessione; `sessions.json` resta leggibile dalla v1.0.0 (campo in più ignorato); il workspace non finisce mai nell'export.

**Scale/Scope**: ~1-20 schede aperte, buffer ≤ 100.000 righe per sessione.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

`.specify/memory/constitution.md` è ancora il template non compilato: nessun principio vincolante. Gate di default: semplicità (nessun nuovo progetto, nessun framework MVVM/DI, nessuna nuova dipendenza), test sulla logica non-UI. **PASS** (pre e post design): unica nuova classe `ContextFilter` (logica pura, testabile), una nuova risorsa (`Guida.html`).

## Project Structure

### Documentation (this feature)

```text
specs/002-usability-improvements/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── sessions-file.md   # campo workspace
│   └── ui.md              # comandi, scorciatoie, badge, guida
└── tasks.md               # /speckit-tasks
```

### Source Code (repository root)

```text
src/RemoteLogViewer/
├── LogTailer.cs          # scansione byte all'indietro, _head, ReadBefore, HasOlder
├── ActiveSession.cs      # IsEntry/IsHistory sulle righe, RequestOlder, coda Older, CanLoadOlder
├── LogLevel.cs           # LogLine: IsEntry, IsHistory, IsContext (notify)
├── ContextFilter.cs      # NUOVO: grep -C N incrementale
├── LogView.xaml(.cs)     # contesto, contatori, salva, carica precedenti, UnreadErrors, ErrorsArrived
├── MainWindow.xaml(.cs)  # badge, FlashWindowEx, workspace save/restore, F1 + pulsante Guida
├── SessionTree.cs        # Workspace / OpenTab + proprietà SessionTree.Workspace
├── SessionStore.cs       # (de)serializza workspace
├── Guida.html            # NUOVO: guida utente (EmbeddedResource)
└── RemoteLogViewer.csproj

tests/RemoteLogViewer.Tests/
├── LogTailerTests.cs     # ReadBefore
├── ContextFilterTests.cs # NUOVO
├── SessionStoreTests.cs  # workspace round-trip, v1 senza workspace
└── LogLevelTests.cs      # IsEntry
```

**Structure Decision**: progetto unico esistente; una classe e una risorsa nuove.

## Complexity Tracking

Nessuna violazione.
