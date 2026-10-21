# Implementation Plan: Analisi dei log con l'IA tramite strumenti CLI

**Branch**: `feature/003-ai-log-analysis` | **Date**: 2026-10-21 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/003-ai-log-analysis/spec.md`

## Summary

Finestra non modale `AiWindow` (Ctrl+Maiusc+I, toolbar "Analisi IA…") che invia le righe visibili delle schede scelte, precedute da un prompt (preset "Report errori"/"Riepilogo" o libero), a una CLI IA esterna (Claude Code, Codex, OpenCode, Gemini CLI/Antigravity, personalizzato). La CLI gira in `cmd /c` in una cartella temporanea, riceve tutto su stdin, in modalità non interattiva e sola lettura; l'output compare progressivamente, si può annullare (kill dell'albero), copiare, aprire formattato nel browser o esportare in `.md`/`.html`.

Approccio: logica pura e testata (`AiInput` limite 1 MB water-filling, `AiSettings` persistenza in `ai.json`, `MarkdownHtml` convertitore minimo, `AiReport` export), esecuzione processo isolata in `AiRunner` (testata con comandi di sistema), UI sottile. Nessuna nuova dipendenza.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (`net10.0-windows`), WPF

**Primary Dependencies**: nessuna esterna (`System.Diagnostics.Process`, `System.Text.Json`, `Regex`, `SaveFileDialog` già nel framework)

**Storage**: `%APPDATA%\RemoteLogViewer\ai.json` (impostazioni IA, mai esportate); cartelle temporanee `%TEMP%\RemoteLogViewer\ai-<guid>` (cancellate a fine esecuzione) e `ai-report.html` (Apri formattato)

**Testing**: xUnit in `tests/RemoteLogViewer.Tests`; logica pura + `AiRunner` con `findstr`/`ping`/comando inesistente; UI e CLI reali a mano con `quickstart.md`

**Target Platform**: Windows 10/11 desktop

**Project Type**: desktop-app

**Performance Goals**: UI sempre reattiva (lettura output asincrona, accodamento su timer UI 200 ms); annullamento ≤ 2 s (SC-003)

**Constraints**: max 1.000.000 caratteri di log per analisi; una sola analisi per volta; nessun accesso dell'IA ai file utente (stdin + cwd temporanea vuota + flag sola lettura/senza tool); offline per HTML

**Scale/Scope**: ≤ ~10 schede, fino a 100.000 righe ciascuna; risposte di qualche decina di KB

## Constitution Check

`.specify/memory/constitution.md` è ancora il template non compilato: nessun gate formale. Regole di `CLAUDE.md` rispettate:

- Nessuna dipendenza NuGet (Markdown → HTML scritto a mano, minimo).
- Stringhe nuove in `L.cs` (`En`), `{local:T}` in XAML, `L.Changed` per testo da codice con unsubscribe su `Closed`; prompt preset localizzati.
- `Checked`/`Unchecked` (non `Click`) per le caselle delle schede, guardia `IsLoaded`.
- `LogLine` intatto; si passano solo i testi.
- Guide it/en (stessi id sezione, tabella scorciatoie completa), `README.md`, `README.it.md`, `CLAUDE.md` aggiornati.

Post-design: nessuna violazione.

## Project Structure

### Documentation (this feature)

```text
specs/003-ai-log-analysis/
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
├── Ai.cs                # NEW  AiTool, AiPrompt, AiSettings (load/save ai.json), AiInput.Build, AiReport (md/html)
├── AiRunner.cs          # NEW  cmd /c, stdin, streaming stdout, stderr, kill albero, temp dir
├── MarkdownHtml.cs      # NEW  Markdown minimo → HTML (pura)
├── AiWindow.xaml(.cs)   # NEW  finestra non modale (contracts/ui.md)
├── LogView.xaml.cs      # + VisibleTexts()
├── MainWindow.xaml(.cs) # + comando Ai, Ctrl+Maiusc+I, pulsante toolbar, singleton AiWindow
├── L.cs                 # + stringhe En
├── Guida.it.html / Guida.en.html   # + sezione "Analisi IA" (stesso id), scorciatoia
tests/RemoteLogViewer.Tests/
├── AiTests.cs           # NEW  AiInput, AiSettings, AiReport
├── AiRunnerTests.cs     # NEW
└── MarkdownHtmlTests.cs # NEW
README.md, README.it.md, CLAUDE.md
```

**Structure Decision**: progetto unico esistente; tipi IA puri raccolti in un solo file `Ai.cs` (piccoli, coesi), processo e Markdown in file propri perché testati separatamente; UI in una finestra nuova sul modello di `SearchWindow`.

## Complexity Tracking

Nessuna violazione da giustificare.
