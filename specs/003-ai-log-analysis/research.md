# Research: Analisi dei log con l'IA tramite strumenti CLI

## R1 — Come consegnare prompt e log alla CLI

- **Decision**: tutto (prompt + intestazioni + righe) viaggia su **stdin** in UTF-8 senza BOM; la riga di comando contiene solo flag fissi. Lo stdin viene chiuso dopo la scrittura.
- **Rationale**: tutte e quattro le CLI leggono stdin in modalità non interattiva (verificato su docs/`--help`, ottobre 2026); nessun limite di lunghezza della riga di comando (32 K su Windows), nessun quoting del prompt, nessun file da far leggere all'agente (che richiederebbe permessi di lettura file).
- **Alternatives**: file temporaneo + prompt "leggi logs.txt" → richiede tool di lettura abilitati, contraddice la sola lettura senza strumenti; prompt come argomento → quoting `cmd`/`CommandLineToArgv` fragile e limite di lunghezza.

## R2 — Comandi preset

| Strumento | Comando predefinito | Note |
|---|---|---|
| Claude Code | `claude -p --tools "" --no-session-persistence` | `-p` senza argomento usa stdin come prompt; `--tools ""` disabilita tutti gli strumenti (nessun accesso a file/comandi); niente sessione salvata. |
| Codex | `codex exec --skip-git-repo-check --sandbox read-only --ephemeral -` | `-` = prompt da stdin; la cartella temporanea non è un repo git; sandbox in sola lettura; stdout = solo messaggio finale, avanzamento su stderr. |
| OpenCode | `opencode run --agent plan --print-logs --log-level WARN` | senza `message` legge stdin; l'agente `plan` è in sola lettura; le domande interattive sono negate in modalità non interattiva. `--print-logs --log-level WARN`: senza, gli errori del provider (es. rate limit Requesty) vengono ritentati in silenzio e lo strumento sembra bloccato. |
| Antigravity CLI | `agy --add-dir . -p="Read the file input.md in the current directory and follow the instructions it contains."` | `agy -p` **ignora lo stdin di testo** (verificato: non vede il contenuto) e accetta solo `-p` o NDJSON con `--input-format stream-json`; con `--mode plan` esplora il PC con comandi shell (negati in headless) e non risponde. In modalità predefinita legge/scrive solo nel workspace (cartella temporanea) e nega i comandi shell: `AiRunner` scrive l'input anche in `input.md`. |
| Personalizzato | `claude -p` | qualunque comando che legge stdin e scrive stdout. |

- **Antigravity**: l'eseguibile `antigravity` è il launcher dell'editor; la CLI è `agy` (`%LOCALAPPDATA%\agy\bin`, docs `antigravity.google/docs/cli/headless`). Gemini CLI è deprecato (account individuali: `IneligibleTierError`) e non è più un preset.

## R3 — Esecuzione del processo

- **Decision**: `cmd.exe /d /s /c "<comando>"`, `UseShellExecute=false`, `CreateNoWindow=true`, stdin/stdout/stderr rediretti, encoding UTF-8, `WorkingDirectory` = cartella nuova in `%TEMP%\RemoteLogViewer\ai-<guid>` cancellata alla fine. Env `NO_COLOR=1`. Annullamento = `Process.Kill(entireProcessTree: true)`.
- **Rationale**: le CLI npm sono shim `.cmd` (`gemini.cmd`, `codex.cmd`), che `Process.Start` senza shell non risolve; `cmd /c` applica PATH+PATHEXT e dà all'utente la stessa sintassi del prompt dei comandi. `/s` + virgolette esterne preserva le virgolette interne del comando. Kill dell'albero perché `cmd` → `node` → figli.
- **Comando non trovato**: il codice d'uscita di `cmd` non è affidabile (su questa build di Windows 11 è 1, non 9009), quindi prima dell'avvio `AiRunner.CanResolve` risolve il primo token come fa `cmd` (comando interno o file su PATH con estensione PATHEXT); se non risolto non parte e restituisce 9009 → messaggio localizzato "Comando non trovato: …".
- **Verifica reale (2026-10-21)**: preset Claude Code e OpenCode rispondono; Antigravity CLI risponde leggendo `input.md`; Gemini CLI (rimosso) dava `IneligibleTierError` per l'account individuale.
- **Streaming**: lettura `StandardOutput.ReadAsync(char[])` a blocchi, accodati e mostrati dal thread UI; stderr letto riga per riga in parallelo: l'ultima riga non vuota compare nello stato durante l'esecuzione (un provider che rifiuta e lo strumento che ritenta non sembrano più un blocco), gli ultimi 4 KB nella risposta se exit ≠ 0 o nessun output. Sequenze ANSI rimosse con regex (`\x1B\[[0-9;?]*[ -/]*[@-~]`).
- **Nota**: Claude Code `-p` in formato `text` stampa la risposta alla fine (non token per token); è il comportamento dello strumento, accettato (SC-002 = mostrare appena lo strumento produce). `stream-json` richiederebbe parsing per strumento: fuori ambito.

## R4 — Limite 1 MB

- **Decision**: budget = 1.000.000 caratteri UTF-16 (≈1 MB) sul solo testo dei log; ripartizione equa tra le schede selezionate, la quota non usata da schede piccole passa alle altre (water-filling), per ogni scheda si tengono le righe più recenti. Funzione pura `AiInput.Build`.
- **Rationale**: misurare caratteri evita encoding ripetuti; equità tra schede evita che una scheda enorme oscuri le altre.

## R5 — Markdown → HTML

- **Decision**: convertitore minimo puro `MarkdownHtml.ToHtml` (titoli `#`, paragrafi, `**grassetto**`, `*corsivo*`, `` `codice` ``, blocchi ``` ``` ```, elenchi `-`/`*`/`1.`, tabelle `|`, righe `---`); tutto il testo HTML-escaped; CSS inline; nessuna risorsa esterna. Pagina = stessa intestazione dell'export. "Apri formattato" scrive `%TEMP%\RemoteLogViewer\ai-report.html` e lo apre con la shell (come la Guida).
- **Alternatives**: Markdig (NuGet) → viola "nessuna dipendenza"; markdown-it da CDN → richiede internet (spec: offline).

## R6 — Persistenza impostazioni

- **Decision**: `%APPDATA%\RemoteLogViewer\ai.json` (camelCase, scrittura atomica temp+move come `SessionStore`): `selectedTool`, `commands` (solo quelli modificati, per id), `lastPrompt`, `consented` (id strumenti). File corrotto → default.
- **Rationale**: separato da `sessions.json` → mai esportato con le sessioni, nessun impatto su import/export/normalize.

## R7 — Scorciatoia

- **Decision**: `Ctrl+Maiusc+I` (libera; `Ctrl+Maiusc+F` = Cerca). Comando `MainWindow.Ai` (`RoutedCommand`), pulsante toolbar "Analisi IA…".
