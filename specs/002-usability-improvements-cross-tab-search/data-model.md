# Data Model: Ricerca avanzata

Tutto in memoria, nessuna persistenza.

## SearchCriteria (record)

| Campo | Tipo | Note |
|---|---|---|
| `Text` | string | vuoto = nessun filtro testo |
| `IsRegex` | bool | falso → testo escapato |
| `MatchCase` | bool | default false |
| `WholeWord` | bool | default false |
| `Levels` | `HashSet<LogLevel>?` | null o tutti e 4 = nessun filtro; con filtro attivo le righe `None` sono escluse |
| `From`, `To` | `DateTime?` | estremi inclusi, entrambi facoltativi |

Regole: `IsEmpty` = testo vuoto, nessun filtro livelli, nessun intervallo → la ricerca non elenca nulla (FR-007). Regex non valida → `ArgumentException` dal costruttore del matcher, gestita dalla UI (messaggio, risultati precedenti intatti).

## SearchSource (record)

`(string Name, IReadOnlyList<LogLine> Lines, DateTime DefaultDate)` — un'istantanea per sessione; `Name` = nome scheda (`DisplayName`), `DefaultDate` = `Session.Date ?? today`.

## SearchHit (record)

| Campo | Tipo |
|---|---|
| `Source` | indice nella lista di `SearchSource` |
| `Line` | `LogLine` (riferimento, identità per il salto) |
| `Start`, `Length` | int — span della prima corrispondenza (0,0 se testo vuoto) |

## SearchResult

`Hits: IReadOnlyList<SearchHit>` (raggruppati per `Source`, ordine di buffer), `Truncated: bool` (limite 10.000 raggiunto).

## ResultRow (UI)

Riga della lista piatta: `IsHeader`, `Header` ("Nome · N") oppure `Hit` + `Pre/Match/Post` + `Level`.

## LogTimestamp

`TryParse(string line, DateTime defaultDate, out DateTime value)`; formati in [research.md](research.md#3-timestamp-decisione-lasciata-aperta-dal-clarify).

## Transizioni

`Idle → Searching (cancellabile) → Done | Error(regex)`. Una nuova ricerca annulla la precedente. Chiudere/aprire sessioni non modifica i risultati già mostrati; il salto verifica che la riga sia ancora in `_all`.
