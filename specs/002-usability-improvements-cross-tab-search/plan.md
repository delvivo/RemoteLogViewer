# Implementation Plan: Ricerca avanzata tra tutti i tab aperti

**Branch**: `feature/002-usability-improvements` | **Date**: 2026-10-01 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/002-usability-improvements-cross-tab-search/spec.md`

## Summary

Finestra separata non modale (`SearchWindow`) che cerca testo/regex nei buffer `_all` di tutte le `LogView` aperte, con maiuscole/minuscole, parola intera, livelli e intervallo data+ora. Risultati raggruppati per sessione in una lista virtualizzata (max 10.000), doppio click → `MainWindow` seleziona la scheda e `LogView.Reveal` azzera i filtri e porta la riga in vista; esportazione su file.

Approccio: la logica è pura e testabile (`LogSearch` + `LogTimestamp`, nessun tipo WPF); la finestra lavora su un'istantanea (`LogLine[]`) presa sul thread UI e cerca in `Task.Run` con annullamento. Nessuna nuova dipendenza.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (`net10.0-windows`), WPF

**Primary Dependencies**: nessuna esterna (solo framework WindowsDesktop; `Regex` e `SaveFileDialog` già in uso)

**Storage**: N/A (nessuna persistenza; ricerche recenti fuori ambito)

**Testing**: xUnit in `tests/RemoteLogViewer.Tests` (`dotnet test`), test sulla logica pura; UI verificata a mano con `quickstart.md`

**Target Platform**: Windows 10/11 desktop

**Project Type**: desktop-app

**Performance Goals**: 5 sessioni × 100.000 righe: primi risultati ≤ 3 s, UI mai bloccata (SC-002); salto ≤ 1 s (SC-003)

**Constraints**: max 10.000 risultati in totale; timeout regex 50 ms per riga (come il filtro esistente); sola lettura dei buffer in memoria, nessun I/O remoto

**Scale/Scope**: ≤ ~10 sessioni aperte, 100.000 righe/buffer (`MaxLines`)

## Constitution Check

`.specify/memory/constitution.md` è ancora il template non compilato: nessun gate formale. Si applicano le regole di `CLAUDE.md`, rispettate così:

- Nessuna dipendenza NuGet nuova.
- Ogni stringa UI nuova in `L.cs` (tabella `En`), `{local:T}` in XAML, `L.Bind`/`L.Changed` per testo impostato da codice sulla finestra a lunga vita (con unsubscribe alla chiusura).
- `Checked`/`Unchecked` (non `Click`) con guardia `IsLoaded`.
- `LogLine` resta classe: i risultati tengono il riferimento all'oggetto, l'identità dà il salto esatto.
- Guide it/en (stessi id sezione), `README.md`, `README.it.md`, `CLAUDE.md` aggiornati.

Post-design: nessuna violazione.

## Project Structure

### Documentation (this feature)

```text
specs/002-usability-improvements-cross-tab-search/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── ui.md
├── checklists/requirements.md
└── tasks.md             # /speckit-tasks
```

### Source Code (repository root)

```text
src/RemoteLogViewer/
├── LogTimestamp.cs        # NEW  parsing timestamp a inizio riga (pura)
├── LogSearch.cs           # NEW  SearchCriteria, SearchHit, Run(...) (pura)
├── SearchWindow.xaml(.cs) # NEW  finestra non modale, risultati, export
├── LogView.xaml.cs        # + Snapshot(), Reveal(LogLine)
├── MainWindow.xaml(.cs)   # + pulsante "Cerca…", Ctrl+Maiusc+F, singleton SearchWindow, selezione scheda
├── L.cs                   # + stringhe En
├── Guida.it.html / Guida.en.html
tests/RemoteLogViewer.Tests/
├── LogTimestampTests.cs   # NEW
└── LogSearchTests.cs      # NEW
README.md, README.it.md, CLAUDE.md   # aggiornati
```

**Structure Decision**: progetto unico esistente; logica pura in due file nuovi (stesso stile di `ContextFilter`/`DatePath`), UI in una finestra nuova. Nessun nuovo progetto.

## Complexity Tracking

Nessuna violazione da giustificare.
