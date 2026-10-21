---
description: "Task list: analisi dei log con l'IA tramite strumenti CLI"
---

# Tasks: Analisi dei log con l'IA tramite strumenti CLI

**Input**: `specs/003-ai-log-analysis/` (plan.md, spec.md, research.md, data-model.md, contracts/ui.md, quickstart.md)

**Tests**: solo sulla logica pura e su `AiRunner` (xUnit in `tests/RemoteLogViewer.Tests/`), come da plan. UI e CLI reali si validano con quickstart.md.

## Format: `[ID] [P?] [Story] Description`

Ogni stringa UI nuova va aggiunta alla tabella `En` di src/RemoteLogViewer/L.cs (chiave = testo italiano); in XAML `{local:T '…'}`; testo impostato da codice aggiornato su `L.Changed` con unsubscribe in `Closed`.

## Phase 1: Setup

- [X] T001 Baseline: `taskkill //IM RemoteLogViewer.exe //F` se serve, poi `dotnet build` e `dotnet test` verdi prima delle modifiche (repo root)

## Phase 2: Foundational (blocca tutte le story)

- [X] T002 Creare src/RemoteLogViewer/Ai.cs con i tipi di data-model.md: `record AiTool(string Id, string Name, string DefaultCommand)` con `static IReadOnlyList<AiTool> All` nell'ordine `claude`, `codex`, `opencode`, `gemini`, `custom` e i comandi esatti di research.md R2 (`claude -p --tools "" --no-session-persistence`; `codex exec --skip-git-repo-check --sandbox read-only --ephemeral -`; `opencode run --agent plan --print-logs --log-level WARN`; `gemini --approval-mode plan -p "Follow the instructions above."`; custom `claude -p`), nomi "Claude Code", "Codex", "OpenCode", "Gemini CLI (Antigravity)", "Personalizzato" — `Name` è una proprietà calcolata a ogni lettura (`L.T` per "Personalizzato"), non un valore fissato all'inizializzazione statica, così segue il cambio lingua; `record AiSource(string Name, string Path, IReadOnlyList<string> Lines)`; `record AiInput(string Text, int Dropped)` con `static AiInput Build(string prompt, IReadOnlyList<AiSource> sources, int maxChars = 1_000_000)` (corpo `throw new NotImplementedException()` per ora)
- [X] T003 [P] Test in tests/RemoteLogViewer.Tests/AiTests.cs per `AiInput.Build`: tutto sotto limite → `Dropped == 0`, testo = prompt + riga vuota + `=== {Name} ({Path}) ===` + righe per ogni sorgente in ordine; oltre limite → restano le righe **più recenti** di ciascuna sorgente; water-filling: sorgente piccola intera, la quota avanzata va alla grande; budget conta caratteri riga +1 per newline e non supera `maxChars`; con `Dropped > 0` il testo contiene, prima dei log, l'avviso con il numero di righe omesse; sorgente con 0 righe → solo intestazione
- [X] T004 Implementare `AiInput.Build` in src/RemoteLogViewer/Ai.cs secondo data-model.md (budget = `maxChars` sulle sole righe; quota equa = residuo / sorgenti rimanenti, in ordine di dimensione crescente; per sorgente le ultime righe che stanno nella quota; avviso localizzato `L.F("({0} righe più vecchie omesse per limite di dimensione)", n)`)
- [X] T005 Creare src/RemoteLogViewer/AiRunner.cs: `sealed class AiRunner` con `Task<AiRunResult> RunAsync(string command, string stdin, Action<string> onOutput, CancellationToken ct)`; `record AiRunResult(int? ExitCode, string Error, bool Cancelled)`. Avvio `cmd.exe` con `ArgumentList`-free `Arguments = "/d /s /c \"" + command + "\""`, `UseShellExecute=false`, `CreateNoWindow=true`, redirect stdin/stdout/stderr, `StandardInput/Output/ErrorEncoding = new UTF8Encoding(false)`, `Environment["NO_COLOR"]="1"`, `WorkingDirectory` = nuova cartella `%TEMP%\RemoteLogViewer\ai-<guid>`; scrittura stdin in background poi `Close()` (ignorare `IOException` se il processo è già uscito); lettura stdout con `ReadAsync(char[4096])` → `onOutput` con sequenze ANSI rimosse (`\x1B\[[0-9;?]*[ -/]*[@-~]`); stderr letto **in parallelo** avviando `StandardError.ReadToEndAsync()` subito dopo `Start()` e prima del ciclo su stdout (altrimenti la pipe piena blocca il processo, es. avanzamento di Codex), `Error` = ultimi 4 KB; annullamento → `Kill(entireProcessTree: true)`, `Cancelled = true`, `ExitCode = null`; `finally` cancella la cartella temporanea (best effort)
- [X] T006 Test in tests/RemoteLogViewer.Tests/AiRunnerTests.cs (dopo T005): `findstr /n "^"` riceve lo stdin e lo restituisce (anche `è`/`à` integri), `ExitCode == 0`; comando che scrive > 64 KB su stderr (es. `cmd /c "for /l %i in (1,1,3000) do @echo errore riga %i 1>&2"`) termina senza bloccarsi e `Error.Length <= 4096`; comando `nonesiste_xyz` → `ExitCode == 9009`; `ping -n 30 127.0.0.1` annullato dopo 300 ms → `Cancelled`, il task termina entro 2 s; cartella temporanea assente dopo l'esecuzione
- [X] T007 [P] In src/RemoteLogViewer/LogView.xaml.cs aggiungere `public string[] VisibleTexts() => _visible.Select(l => l.Text).ToArray();` (thread UI; stessi testi di Ctrl+S) e usarlo in `SaveVisible`

**Checkpoint**: input e runner pronti e testati.

---

## Phase 3: User Story 1 - Report degli errori generato dall'IA (P1) 🎯 MVP

**Goal**: scegliere schede e strumento, avviare "Report errori", vedere la risposta man mano, annullare.

**Independent Test**: file locale con ERROR ripetuti → Ctrl+Maiusc+I, scheda spuntata, strumento installato, "Report errori", Avvia → report nella finestra (quickstart §2–3).

- [X] T008 [US1] In src/RemoteLogViewer/Ai.cs aggiungere `record AiPrompt(string Name, string Text)` con `static IReadOnlyList<AiPrompt> All()` (ricostruita a ogni chiamata, lingua attiva): "Report errori" (testo `L.T`: errori/eccezioni raggruppati per causa, numero di occorrenze, prima e ultima occorrenza con timestamp, schede coinvolte, possibili cause, azioni suggerite; rispondi in Markdown nella lingua di queste istruzioni) e "Riepilogo" (stato generale, eventi rilevanti, anomalie, in Markdown)
- [X] T009 [US1] Creare src/RemoteLogViewer/AiWindow.xaml e .xaml.cs (layout di contracts/ui.md, ridimensionabile, non modale): costruttore `(Func<IReadOnlyList<LogView>> views, LogView? active)`; colonna sinistra `ItemsControl` di `CheckBox` (contenuto `Session.DisplayName`, `Checked`/`Unchecked` con guardia `IsLoaded`) ricostruita su `Activated` mantenendo la selezione per oggetto `LogView`, `active` preselezionata; `ComboBox` strumento (per ora solo selezione, comando = `DefaultCommand`, `TextBox` comando in sola lettura); `ComboBox` prompt preset che sostituisce il testo di `PromptBox` (multilinea), default "Report errori"; avviso fisso `{local:T 'I log selezionati vengono inviati allo strumento IA scelto e, per gli strumenti online, al relativo servizio.'}`; pulsanti Avvia/Annulla; etichetta stato; `OutputBox` `TextBox` sola lettura, Consolas, scroll, `TextWrapping=Wrap`; su `L.Changed` ricostruire le voci delle `ComboBox` strumento e prompt (nomi nella nuova lingua) mantenendo la selezione per indice, senza toccare il testo di `PromptBox`
- [X] T010 [US1] In src/RemoteLogViewer/AiWindow.xaml.cs logica di Avvia: abilitato solo se nessuna analisi in corso, ≥1 scheda spuntata ancora aperta, prompt e comando non vuoti; cattura sul thread UI `AiSource(Session.DisplayName, Session.Path, view.VisibleTexts())` per le schede spuntate (schede chiuse ignorate); `AiInput.Build`; svuota `OutputBox`; `AiRunner.RunAsync` con `CancellationTokenSource`; `onOutput` accoda a una `ConcurrentQueue<string>` svuotata da `DispatcherTimer` 200 ms in `OutputBox.AppendText` (+ `ScrollToEnd`) che aggiorna anche il tempo trascorso; a fine: stati di contracts/ui.md (`Completato in mm:ss`, `Annullato`, `Errore (codice N)` con `Error` (già limitato a 4 KB) accodato a `OutputBox`, 9009 → `L.F("Comando non trovato: {0}", comando)`, output vuoto → `Nessuna risposta`) + `L.F("{0} righe escluse per limite 1 MB", Dropped)` se > 0; stato come `Func<string>` rinfrescato su `L.Changed`. Annulla → `cts.Cancel()`. `Closed` → cancel, stop timer, unsubscribe `L.Changed`
- [X] T011 [US1] In src/RemoteLogViewer/MainWindow.xaml aggiungere `CommandBinding`/`KeyBinding Key="I" Modifiers="Control+Shift"` per `MainWindow.Ai` e il pulsante toolbar `{local:T 'Analisi IA…'}` con tooltip `{local:T 'Analizza i log con uno strumento IA (Ctrl+Maiusc+I)'}` accanto a "Cerca…"; in src/RemoteLogViewer/MainWindow.xaml.cs `public static readonly RoutedCommand Ai = new();` e `AiWindow? _ai` singleton come `_search` (ripristina se minimizzata, `Activate`), `active` = `Tag` della scheda selezionata, `Owner = this`
- [X] T011a [US1] Consenso (FR-013) in src/RemoteLogViewer/Ai.cs + AiWindow.xaml.cs: aggiungere ora `sealed class AiSettings` minima con `List<string> Consented`, `DefaultPath` (`ai.json` accanto a `SessionStore.DefaultPath`), `Load`/`Save` (completata in T019); prima dell'avvio, se `!Consented.Contains(id)` → `MessageBox` Sì/No `L.F("I log selezionati verranno inviati a {0}. Per gli strumenti online i dati lasciano questo PC. Continuare?", Name)`; Sì → aggiungi e salva; No → non parte
- [X] T012 [P] [US1] Aggiungere a src/RemoteLogViewer/L.cs `En` tutte le stringhe di T004, T008–T011a

**Checkpoint**: report errori funzionante con lo strumento predefinito. MVP.

---

## Phase 4: User Story 2 - Esportare il report (P1)

**Goal**: Copia, Apri formattato (HTML nel browser), Esporta `.md`/`.html`.

**Independent Test**: analisi completata → Esporta `.md` e `.html`, Apri formattato (quickstart §4).

- [X] T013 [P] [US2] Test in tests/RemoteLogViewer.Tests/MarkdownHtmlTests.cs: `#`–`######` → `<h1>`–`<h6>`; paragrafi; `**b**` → `<strong>`, `*i*` → `<em>`, `` `c` `` → `<code>`; blocco ``` ``` ``` → `<pre><code>` senza formattazione interna; elenchi `-`/`*` → `<ul><li>`, `1.` → `<ol><li>`; tabella `| a | b |` + `|---|---|` → `<table>` con `<th>`/`<td>`; `---` → `<hr>`; `<script>` e `&` escaped ovunque (anche nel codice)
- [X] T014 [US2] Creare src/RemoteLogViewer/MarkdownHtml.cs: `static class MarkdownHtml` con `string ToHtml(string markdown)` (solo frammento body) che soddisfa T013; riga per riga, nessuna dipendenza, `WebUtility.HtmlEncode` prima delle sostituzioni inline
- [X] T015 [US2] In src/RemoteLogViewer/Ai.cs aggiungere `record AiReport(DateTime Time, string Tool, string Command, IReadOnlyList<string> Sources, string Prompt, int Dropped, string Response)` con `ToMarkdown()` (`# ` + `L.T("Analisi IA")`, data/ora, strumento + comando, schede, prompt in blockquote, righe escluse se > 0, `---`, risposta integrale) e `ToHtml()` (pagina autonoma: `<!doctype html>`, `<meta charset="utf-8">`, `lang` = `L.Lang`, `<title>`, CSS inline per tabelle/codice/font, body = `MarkdownHtml.ToHtml(ToMarkdown())`)
- [X] T016 [P] [US2] Test `AiReport` in tests/RemoteLogViewer.Tests/AiTests.cs: `ToMarkdown` contiene strumento, schede, prompt e risposta integrale; `ToHtml` inizia con `<!doctype html>`, contiene `charset="utf-8"` e nessun `http`
- [X] T017 [US2] In src/RemoteLogViewer/AiWindow.xaml(.cs) pulsanti Copia / Apri formattato / Esporta…, abilitati solo con risposta non vuota e nessuna analisi in corso; `AiReport` costruito dai dati catturati all'avvio; Copia → `Clipboard.SetText(Response)`; Apri formattato → scrive `%TEMP%\RemoteLogViewer\ai-report.html` (UTF-8 senza BOM) e lo apre con `Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })`; Esporta → `SaveFileDialog` filtri `Markdown (*.md)|*.md|HTML (*.html)|*.html`, nome default `ai-report-yyyyMMdd-HHmm`, scrive `ToMarkdown`/`ToHtml` in base all'estensione, UTF-8 senza BOM; errori I/O → `MessageBox`
- [X] T018 [P] [US2] Stringhe nuove di T015/T017 in src/RemoteLogViewer/L.cs `En`

**Checkpoint**: US1 + US2 = report utilizzabile ed esportabile.

---

## Phase 5: User Story 3 - Scegliere lo strumento e personalizzare il comando (P2)

**Goal**: preset + comando modificabile e ripristinabile, consenso per strumento, tutto persistito.

**Independent Test**: Personalizzato `findstr /n "^"` → output; chiudi e riapri app → comando ritrovato (quickstart §2, §6).

- [X] T019 [US3] In src/RemoteLogViewer/Ai.cs completare `AiSettings` (da T011a; camelCase JSON): `string SelectedTool = "claude"`, `Dictionary<string,string> Commands`, `string? LastPrompt`, `List<string> Consented`; `string CommandFor(string id)` (override o `DefaultCommand`); `void SetCommand(string id, string cmd)` (uguale al predefinito o vuoto → rimuove l'override); `static string DefaultPath` = `ai.json` accanto a `SessionStore.DefaultPath`; `static AiSettings Load(string path)` (assente/non valido → default, id sconosciuti tollerati); `void Save(string path)` atomico (temp + `File.Move(overwrite: true)`, crea la cartella)
- [X] T020 [P] [US3] Test in tests/RemoteLogViewer.Tests/AiTests.cs: default senza file; round-trip Save/Load; file corrotto → default; `SetCommand` con valore predefinito rimuove l'override e `CommandFor` torna al predefinito; `SelectedTool` sconosciuto non lancia
- [X] T021 [US3] In src/RemoteLogViewer/AiWindow.xaml.cs caricare `AiSettings` all'apertura; `ComboBox` strumento → `SelectedTool`, `TextBox` comando modificabile → `SetCommand` su `LostFocus` e prima dell'avvio; pulsante Ripristina → `SetCommand(id, DefaultCommand)`; salvataggio su cambio e su `Closed`
- [X] T022 [US3] Verificare in src/RemoteLogViewer/AiWindow.xaml.cs che il consenso di T011a sia per **strumento** (cambio strumento → nuova conferma per quello non ancora confermato) e sopravviva al riavvio insieme agli altri campi di `AiSettings`
- [X] T023 [P] [US3] Stringhe nuove di T021 in src/RemoteLogViewer/L.cs `En`

**Checkpoint**: tutti gli strumenti usabili e configurabili.

---

## Phase 6: User Story 4 - Prompt libero (P2)

**Goal**: prompt preset modificabile o libero, ultimo prompt ricordato.

**Independent Test**: domanda libera su un log noto → risposta pertinente; riapertura → prompt ritrovato (quickstart §6).

- [X] T024 [US4] In src/RemoteLogViewer/AiWindow.xaml.cs: `PromptBox` inizializzato da `LastPrompt` (se null → testo "Report errori" nella lingua attiva, preset non selezionato se il testo differisce); `LastPrompt` salvato all'avvio dell'analisi e su `Closed`; Avvia disabilitato con prompt vuoto o di soli spazi

**Checkpoint**: tutte le story complete.

---

## Phase 7: Polish & Cross-Cutting

- [X] T025 [P] Guide src/RemoteLogViewer/Guida.it.html e src/RemoteLogViewer/Guida.en.html: nuova sezione "Analisi IA"/"AI analysis" con lo **stesso id** in entrambe (prerequisiti CLI installate e autenticate, schede/righe visibili, limite 1 MB, preset e comandi, consenso e privacy — i preset non scrivono né eseguono comandi ma alcuni strumenti possono leggere altri file del PC; il comando personalizzato è responsabilità dell'utente —, annulla, copia/apri formattato/esporta), riga `Ctrl+Maiusc+I`/`Ctrl+Shift+I` nella tabella scorciatoie; verificare `GuideTests`
- [X] T026 [P] README.md (inglese) e README.it.md: funzione Analisi IA, scorciatoia, tabella comandi preset, nota su Antigravity
- [X] T027 [P] CLAUDE.md: punto architettura `AiWindow`/`AiRunner`/`AiInput`/`AiSettings` (`ai.json`, mai esportato), stdin + `cmd /d /s /c` + kill albero, `MarkdownHtml` minimo; gotcha: Claude Code `-p` testo non fa streaming, Antigravity senza modalità headless, sola lettura ≠ nessuna lettura (Codex/Gemini/OpenCode possono leggere file del PC; solo Claude `--tools ""` è senza strumenti, da dire anche nelle guide), stderr letto in parallelo, `AiRunnerTests` usa `findstr`/`ping`
- [ ] T028 Verifica finale (build/test verdi ✔; quickstart §2–6 a mano ancora da fare: UI Automation non riesce a pilotare il dialogo file di sistema): `dotnet build` senza warning nuovi, `dotnet test` verde (incluso `LocTests`/`GuideTests`), quickstart.md §2–6 a mano, poi cancellare `%APPDATA%\RemoteLogViewer\ai.json` creato dai test manuali

---

## Phase 8: Correzioni dopo la prova manuale

- [X] T029 OpenCode collegato a Requesty restava >11 min senza output: il provider rifiutava per rate limit e OpenCode ritentava in silenzio. src/RemoteLogViewer/AiRunner.cs: stderr letto riga per riga con callback `onError`; src/RemoteLogViewer/AiWindow.xaml.cs: ultima riga di stderr nello stato durante l'esecuzione (tooltip col testo intero); preset OpenCode con `--print-logs --log-level WARN` (src/RemoteLogViewer/Ai.cs); `CanResolve` accetta comandi che iniziano con `(`; test in tests/RemoteLogViewer.Tests/AiRunnerTests.cs; guide, README, research aggiornati

## Dependencies & Execution Order

- Setup (T001) → Foundational (T002–T007) → US1 (T008–T012, incluso T011a consenso) → US2 (T013–T018) → US3 (T019–T023) → US4 (T024) → Polish (T025–T028).
- US2, US3, US4 dipendono da `AiWindow` (T009/T010) di US1; tra loro sono indipendenti salvo che toccano lo stesso file `AiWindow.xaml.cs` (eseguire in sequenza).
- T004 dopo T003 (test prima); T014 dopo T013; T006 dopo T005; T011a dopo T010.

### Parallel Opportunities

- Foundational: T003 e T007 in parallelo con T005 (file diversi).
- US2: T013 e T016 (test) in parallelo, poi T014/T015.
- US3: T020 in parallelo con T021.
- Polish: T025, T026, T027 in parallelo.
- Le task `L.cs` (T012, T018, T023) sono [P] rispetto ai file della finestra.

## Implementation Strategy

1. **MVP** = Phase 1–3: report errori con lo strumento predefinito, visibile e annullabile, con consenso alla prima analisi.
2. + US2: export/apri formattato (richiesta esplicita dell'utente → da consegnare insieme all'MVP).
3. + US3: tutti gli strumenti, comandi personalizzati, consenso persistito.
4. + US4: prompt libero ricordato.
5. Polish: documentazione e verifica finale.
