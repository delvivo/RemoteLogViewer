# Data Model: Analisi dei log con l'IA

Tutti i tipi sono puri (nessun tipo WPF) salvo dove indicato; file `src/RemoteLogViewer/Ai*.cs`, `MarkdownHtml.cs`.

## AiTool (preset, statico)

| Campo | Tipo | Note |
|---|---|---|
| Id | string | `claude`, `codex`, `opencode`, `antigravity`, `custom`; chiave stabile in `ai.json` |
| Name | string | nome visualizzato (non localizzato, tranne "Personalizzato") |
| DefaultCommand | string | vedi research R2 |

`AiTool.All`: elenco fisso nell'ordine sopra.

## AiPrompt (preset, statico)

| Campo | Tipo | Note |
|---|---|---|
| Name | string | `L.T("Report errori")`, `L.T("Riepilogo")` |
| Text | string | testo nella lingua attiva (`L.T`) |

Il prompt "Report errori" chiede: errori raggruppati per causa, numero occorrenze, prima/ultima occorrenza (timestamp), schede coinvolte, possibili cause, azioni suggerite, in Markdown.

## AiSettings (persistito in `ai.json`)

| Campo | Tipo | Default |
|---|---|---|
| SelectedTool | string | `claude` |
| Commands | Dictionary<string,string> | vuoto (= comando predefinito) |
| LastPrompt | string? | null (= testo di "Report errori") |
| Consented | List<string> | vuoto |

- `CommandFor(id)` = `Commands[id]` se presente, altrimenti `DefaultCommand`. Salvare un comando uguale al predefinito lo rimuove da `Commands` ("Ripristina").
- `Load(path)`: file assente o non valido → default. `Save(path)`: atomico (temp + move).

## AiSource (input per una scheda, catturato all'avvio sul thread UI)

| Campo | Tipo | Note |
|---|---|---|
| Name | string | `ActiveSession.DisplayName` |
| Path | string | `ActiveSession.Path` |
| Lines | IReadOnlyList<string> | testi di `_visible` della `LogView` (come Ctrl+S) |

## AiInput (risultato di `AiInput.Build(prompt, sources, maxChars = 1_000_000)`)

| Campo | Tipo | Note |
|---|---|---|
| Text | string | prompt + riga vuota + per ogni sorgente `=== {Name} ({Path}) ===` + righe incluse |
| Dropped | int | righe escluse in totale (le più vecchie) |

Regole: budget sui caratteri delle righe (+1 per newline); water-filling equo tra sorgenti; per sorgente si includono le ultime righe che stanno nella quota; se `Dropped > 0` il testo indica, prima dei log, quante righe più vecchie sono state omesse.

## AiRun (stato di esecuzione, in `AiRunner`)

Stati: `Idle → Running → (Completed | Cancelled | Failed)`.

| Campo | Tipo | Note |
|---|---|---|
| Output | string | stdout accumulato, sequenze ANSI rimosse |
| ExitCode | int? | null se annullato |
| Error | string | stderr (troncato a 4 KB per la visualizzazione) |
| Elapsed | TimeSpan | |

- `Failed`: exit ≠ 0 (9009 → "Comando non trovato: {comando}") o eccezione di avvio.
- `Completed` con `Output` vuoto → "nessuna risposta".

## AiReport (per export/apri formattato)

| Campo | Tipo |
|---|---|
| Time | DateTime |
| Tool | string (nome) |
| Command | string |
| Sources | nomi schede |
| Prompt | string |
| Response | string |

- `AiReport.ToMarkdown()`: intestazione (`# Analisi IA`, data/ora, strumento, schede, prompt in blockquote, righe escluse) + `---` + risposta integrale.
- `AiReport.ToHtml()` = pagina autonoma: `MarkdownHtml.ToHtml(ToMarkdown())` dentro `<html>` con CSS inline, `<meta charset="utf-8">`, `lang` = lingua attiva.
