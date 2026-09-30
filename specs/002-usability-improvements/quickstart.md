# Quickstart: validazione

## Prerequisiti

- `dotnet build` e `dotnet test` verdi (i test SMB vanno eseguiti fuori dalla sandbox).
- Una share raggiungibile, ad esempio `\\localhost\C$`, con il file `C:\temp\rlv\a.log` (5000 righe `INFO riga i`) e `C:\temp\rlv\b.log`.
- Salva una copia di `%APPDATA%\RemoteLogViewer\sessions.json` e ripristinala alla fine.

Generare i file:

```powershell
New-Item -ItemType Directory -Force C:\temp\rlv | Out-Null
1..5000 | % { "2026-09-30 10:00:00 INFO riga $_" } | Set-Content C:\temp\rlv\a.log
"2026-09-30 10:00:00 INFO start" | Set-Content C:\temp\rlv\b.log
```

## Scenari

1. **Contatori e carica precedenti**: sessione A su `a.log` con 1000 righe iniziali. All'apertura: righe 4001–5000, checkbox `INFO 1000`. *Precedenti* → 3001–5000, `INFO 2000`, la riga in vista non salta. Ripeti fino all'inizio del file: il pulsante si disabilita.
2. **Badge**: apri A e B, resta su A. `Add-Content C:\temp\rlv\b.log "2026-09-30 ERROR boom","   at X.Y()","   at Z.W()"` → sulla scheda B compare `1` in rosso. Seleziona B: il badge sparisce. In Affianca, ripeti: nessun badge.
3. **Lampeggio**: metti in primo piano un'altra app, aggiungi un ERROR a `b.log` → la taskbar lampeggia; torna sull'app → smette.
4. **Contesto**: su A filtro `riga 4500`, contesto 2 → righe 4498–4502 (4500 piena, le altre attenuate). Filtro regex `riga (4500|4510)$` → due gruppi separati da `--`. Deseleziona INFO → nessuna riga.
5. **Salva**: con il filtro del punto 4, Ctrl+S → nome proposto `A_yyyyMMdd_HHmmss.log`; il file contiene 11 righe (5 + `--` + 5).
6. **Workspace**: apri A, B e una sessione con `{date:…}` su una data passata; trascina B per prima, attiva Affianca, chiudi l'app. Riaprendola ritrovi le stesse schede nello stesso ordine, Affianca attivo e la data passata, senza nessun dialog.
7. **Guida**: premi F1 → la guida si apre nel browser e mostra la versione. Riprova con la rete scollegata: funziona lo stesso.
8. **Regressione**: rotazione (`Set-Content a.log "nuovo"`) → marker e lettura da zero, *Precedenti* disabilitato; ricerca F3; *Pulisci* azzera i contatori.
