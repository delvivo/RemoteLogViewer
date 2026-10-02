# Implementation Plan: Apertura di un file log dal file system

**Branch**: `feature/002-usability-improvements` | **Date**: 2026-10-01 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/002-usability-improvements-open-local-file/spec.md`

## Summary

*Apri file…* (`Ctrl+O`, dialogo multi-selezione) e drag&drop sulla finestra aprono un file locale in una nuova scheda. Riuso totale di `ActiveSession` + `LogView`: il file locale è una `SessionConfig` "locale" (non salvata nell'elenco) per cui `ActiveSession` salta la connessione SMB. Tutte le funzioni (filtri, ricerca avanzata, Affianca, avvisi, *Precedenti*, rotazione) restano quelle esistenti. Il workspace ricorda il percorso e riapre i file ancora esistenti.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (`net10.0-windows`), WPF

**Primary Dependencies**: nessuna esterna (`OpenFileDialog` di `Microsoft.Win32`, già in uso per `SaveFileDialog`)

**Storage**: `sessions.json` esistente, solo il campo opzionale `workspace.tabs[].localPath` (v1-compatibile); nessun altro file

**Testing**: xUnit; i test di `ActiveSession` su file locale non usano SMB, quindi girano anche nella sandbox

**Target Platform**: Windows 10/11 desktop

**Project Type**: desktop-app

**Performance Goals**: file da 500 MB: ultime righe ≤ 3 s (SC-002, già garantito da `LogTailer.ReadTail` a blocchi dal fondo); nuove righe entro 2 s (poll 500 ms)

**Constraints**: nessuna connessione di rete aperta/chiusa dall'app per i file locali; apertura con `FileShare.ReadWrite|Delete` (`LogTailer`), mai bloccare chi scrive

**Scale/Scope**: ~5 file toccati, nessuna nuova classe di UI

## Constitution Check

`.specify/memory/constitution.md` è ancora il template: nessun gate formale. Regole di `CLAUDE.md` rispettate: nessuna dipendenza NuGet, stringhe in `L.cs` (`En`), `{local:T}` in XAML, guide it/en (stessi id sezione), `README.md`/`README.it.md`/`CLAUDE.md` aggiornati, nessun `Click` per toggle (qui solo comandi). Post-design: nessuna violazione.

## Project Structure

### Documentation (this feature)

```text
specs/002-usability-improvements-open-local-file/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/ui.md
├── checklists/requirements.md
└── tasks.md             # /speckit-tasks
```

### Source Code (repository root)

```text
src/RemoteLogViewer/
├── SessionConfig.cs       # + IsLocal, Local(path); FullPath/ResolvePath restituiscono FilePath se locale
├── ActiveSession.cs       # nessun SmbConnection.Connect/Release se Config.IsLocal
├── SessionTree.cs         # OpenTab + LocalPath
├── MainWindow.xaml(.cs)   # ApplicationCommands.Open + pulsante, PreviewDragOver/PreviewDrop, OpenLocalFiles, workspace
├── L.cs                   # + stringhe En
├── Guida.it.html / Guida.en.html
tests/RemoteLogViewer.Tests/
├── SessionConfigTests.cs  # + Local()
├── ActiveSessionTests.cs  # + tail di un file locale senza SMB
└── SessionStoreTests.cs   # + roundtrip di OpenTab.LocalPath
README.md, README.it.md, CLAUDE.md
```

**Structure Decision**: nessun file nuovo nel codice di produzione: il file locale è un caso particolare di sessione, non un secondo tipo di scheda.

## Complexity Tracking

Nessuna violazione da giustificare.
