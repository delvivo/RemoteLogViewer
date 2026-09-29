# Implementation Plan: Collezioni, riordino schede e percorsi con data

**Branch**: `feature/001-remote-log-viewer` | **Date**: 2026-09-30 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/001-remote-log-viewer-collections-tabs-datepath/spec.md`

## Summary

Tre estensioni all'app esistente:

1. **Percorso con data**: segnaposto `{date:<formato .NET>}` nel percorso del file, risolto con una data scelta in un piccolo dialog all'avvio. Se la data è oggi, `ActiveSession` passa al file del nuovo giorno a mezzanotte.
2. **Collezioni annidate**: modello piatto (`Collection { Id, Name, ParentId }` + `SessionConfig.CollectionId`) salvato in `sessions.json`. La UI costruisce un `TreeView` ricostruito dopo ogni modifica. Export/import usano lo stesso formato JSON, senza password e con ID rigenerati all'import.
3. **Riordino schede**: drag & drop dei `TabItem` nel `TabControl`, poi `Relayout()`, che già rispetta l'ordine delle schede.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0-windows`)

**Primary Dependencies**: WPF in-box (`TreeView`, `DatePicker`, `Microsoft.Win32.OpenFileDialog`/`SaveFileDialog`), `System.Text.Json`. Nessuna nuova dipendenza.

**Storage**: `%APPDATA%\RemoteLogViewer\sessions.json`, esteso con `collections` e `collectionId` (retro-compatibile). File di export `*.rlv.json`.

**Testing**: xUnit. Logica pura testata: `DatePath` (parse/resolve/validate), `SessionTree` (move/cycle/delete/export/import/nomi univoci), round-trip dello store. UI validata con [quickstart.md](quickstart.md).

**Target Platform**: Windows 10/11 x64

**Project Type**: desktop-app

**Performance Goals**: ricostruzione dell'albero < 50 ms con ~200 elementi (rebuild completo, niente diff); cambio giorno rilevato entro 1 ciclo di polling (≤ 500 ms).

**Constraints**: l'export non deve mai contenere password; l'import non modifica elementi esistenti; il formato di `sessions.json` resta leggibile dalla versione precedente (i campi nuovi vengono ignorati).

**Scale/Scope**: ~10-200 sessioni, profondità dell'albero tipica ≤ 4.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

`.specify/memory/constitution.md` è ancora il template non compilato: nessun principio vincolante. Gate di default: semplicità (nessun nuovo progetto, nessun framework MVVM/DI, nessuna nuova dipendenza), test sulla logica non-UI. **PASS** (pre e post design).

## Project Structure

### Documentation (this feature)

```text
specs/001-remote-log-viewer-collections-tabs-datepath/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── sessions-file.md   # sessions.json v2
│   ├── export-file.md     # file di export/import
│   └── ui.md              # albero, dialog data, drag schede
└── tasks.md               # /speckit-tasks
```

### Source Code (repository root)

```text
src/RemoteLogViewer/
├── SessionConfig.cs        # + CollectionId; FullPath → ResolvePath(DateTime)
├── DatePath.cs             # NUOVO: regex {date:fmt}, Resolve/Has/Validate
├── SessionTree.cs          # NUOVO: Collection + operazioni su albero piatto, export/import
├── SessionStore.cs         # SessionsFile + Collections; Export/Import file
├── ActiveSession.cs        # + data seguita, FollowToday, cambio giorno, DisplayName
├── DateDialog.xaml(.cs)    # NUOVO: DatePicker + anteprima percorso
├── TextDialog.xaml(.cs)    # NUOVO: input nome collezione (WPF non ha InputBox)
├── SessionEditWindow.*     # anteprima percorso risolto + errori formato data
├── MainWindow.xaml(.cs)    # ListBox → TreeView, menu contestuale, drag&drop albero e schede
└── LogView.xaml.cs         # titolo da Session.DisplayName

tests/RemoteLogViewer.Tests/
├── DatePathTests.cs        # NUOVO
├── SessionTreeTests.cs     # NUOVO
└── SessionStoreTests.cs    # + migrazione v1 e round-trip collezioni
```

**Structure Decision**: progetto app + progetto test esistenti. Due nuovi file di logica (`DatePath`, `SessionTree`) tengono fuori dalla UI tutto ciò che è testabile. I dialog sono piccoli `Window` XAML come `SessionEditWindow`.

## Complexity Tracking

Nessuna violazione.
