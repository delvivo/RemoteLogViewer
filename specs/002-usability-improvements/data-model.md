# Data Model: Miglioramenti di usabilità

## Workspace (persistito in `sessions.json`)

| Campo | Tipo | Note |
|---|---|---|
| `tabs` | `OpenTab[]` | nell'ordine delle schede |
| `selected` | `int` | indice della scheda selezionata, `-1` = nessuna |
| `sideBySide` | `bool` | modalità Affianca |

### OpenTab

| Campo | Tipo | Note |
|---|---|---|
| `sessionId` | `Guid` | `SessionConfig.Id`; se non esiste più la scheda viene ignorata |
| `date` | `DateTime?` | data scelta (solo se il percorso ha `{date:}`) |
| `followToday` | `bool` | se `true`, al ripristino si usa la data di oggi |

**Regole**
- Scritto solo alla chiusura della finestra; letto una volta su `Loaded`.
- Mai incluso in export/import. `Normalize()` non lo tocca: gli id orfani vengono scartati al ripristino.
- Un file senza `workspace` (v1.0.0) → workspace vuoto.

## LogLine (runtime, esteso)

| Campo | Tipo | Note |
|---|---|---|
| `IsEntry` | `bool` | livello rilevato sulla riga stessa: inizio di una voce (conta per contatori e badge) |
| `IsHistory` | `bool` | letta all'apertura: non genera avvisi |
| `IsContext` | `bool` (notify) | mostrata come contesto, non come match; opacità ridotta |

Il separatore di contesto è un `LogLine("--", None, isMarker: true)` che esiste solo nella vista.

## LogTailer (stato, esteso)

| Campo | Note |
|---|---|
| `_start` | lunghezza del preambolo/BOM |
| `_head` | offset in byte della prima riga consegnata; `HasOlder = _head > _start` |

**Transizioni di `_head`**: con `ReadTail` = inizio della prima riga restituita; con `ReadBefore(n)` = inizio del nuovo blocco; con la rotazione in `Poll` = `_start`.

## ActiveSession (esteso)

| Membro | Note |
|---|---|
| `RequestOlder(int n)` | chiamabile dalla UI; eseguito dal loop entro ~500 ms |
| `Older` | `ConcurrentQueue<List<LogLine>>`: blocchi da mettere in testa |
| `CanLoadOlder` | `Running && tailer.HasOlder`; se cambia viene sollevato `StateChanged` |

## LogView (stato UI, esteso)

| Membro | Note |
|---|---|
| `_counts[LogLevel]` | voci per livello nel buffer |
| `UnreadErrors` | voci ERROR non storiche arrivate mentre `!IsVisible`; torna a 0 quando la vista diventa visibile |
| `ErrorsArrived` (event) | sollevato con le nuove voci ERROR non storiche (usato per il lampeggio) |
| `_context` | N righe di contesto (0–50), non persistito |
