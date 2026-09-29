# Implementation Plan: Remote Log Viewer

**Branch**: `feature/001-remote-log-viewer` | **Date**: 2026-09-29 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/001-remote-log-viewer/spec.md`

## Summary

App desktop Windows che sostituisce `New-SmbMapping` + `Get-Content -Tail N -Wait`: sessioni salvate (share UNC, credenziali cifrate, file), tail in tempo reale con più sessioni in schede o affiancate, colori per livello, filtro/ricerca, pausa follow. Approccio: WPF su .NET 10, autenticazione SMB tramite `WNetAddConnection2` senza lettera di unità, password DPAPI (CurrentUser), sessioni in JSON in `%APPDATA%`, tail per polling della dimensione file con `FileStream` condiviso.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0-windows`)

**Primary Dependencies**: WPF (in-box), `System.Security.Cryptography.ProtectedData` (Microsoft, DPAPI). Test: xUnit.

**Storage**: file JSON `%APPDATA%\RemoteLogViewer\sessions.json` (password come blob DPAPI base64)

**Testing**: xUnit (`dotnet test`) per tailer, rilevamento livello, store sessioni, filtro. UI validata manualmente con [quickstart.md](quickstart.md).

**Target Platform**: Windows 10/11 x64

**Project Type**: desktop-app

**Performance Goals**: nuove righe visibili ≤ 2 s (polling 500 ms); 5 sessioni × 100 righe/s senza blocchi UI (append in batch sul dispatcher, lista virtualizzata)

**Constraints**: nessuna password in chiaro su disco; lettura solo della coda del file; buffer max 100.000 righe/sessione

**Scale/Scope**: uso personale, ~10-50 sessioni salvate, fino a ~10 attive

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

`.specify/memory/constitution.md` è ancora il template non compilato: nessun principio vincolante. Gate applicati di default: semplicità (2 progetti, nessun framework MVVM/DI esterno), test sulla logica non-UI. **PASS** (pre e post design).

## Project Structure

### Documentation (this feature)

```text
specs/001-remote-log-viewer/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── sessions-file.md
│   └── ui.md
└── tasks.md
```

### Source Code (repository root)

```text
RemoteLogViewer.sln
src/RemoteLogViewer/
├── RemoteLogViewer.csproj
├── App.xaml(.cs)
├── MainWindow.xaml(.cs)          # elenco sessioni, schede, vista affiancata
├── SessionEditWindow.xaml(.cs)   # form sessione + "Test connessione"
├── LogView.xaml(.cs)             # vista di una sessione attiva (lista, filtro, ricerca, follow)
├── SessionConfig.cs              # modello persistito
├── SessionStore.cs               # load/save JSON + DPAPI
├── SmbConnection.cs              # P/Invoke WNetAddConnection2 / WNetCancelConnection2
├── LogTailer.cs                  # lettura coda + polling, rotazione, encoding
├── LogLevel.cs                   # rilevamento livello da testo
└── ActiveSession.cs              # stato runtime, buffer, riconnessione
tests/RemoteLogViewer.Tests/
├── RemoteLogViewer.Tests.csproj
├── LogTailerTests.cs
├── LogLevelTests.cs
└── SessionStoreTests.cs
```

**Structure Decision**: un progetto app WPF + un progetto test xUnit. Nessuna libreria separata: la logica testabile (tailer, livelli, store) vive in classi senza dipendenze WPF nel progetto app, referenziato dal progetto test.

## Complexity Tracking

Nessuna violazione.
