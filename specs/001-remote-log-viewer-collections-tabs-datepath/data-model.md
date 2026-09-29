# Data Model: Collezioni, riordino schede e percorsi con data

## Collection (nuova)

| Campo | Tipo | Note |
|---|---|---|
| `Id` | Guid | generato |
| `Name` | string | obbligatorio, trim; univoco tra le collezioni sorelle (case-insensitive) nella creazione/rinomina manuale |
| `ParentId` | Guid? | null = radice; deve riferire una collezione esistente |

**Invarianti**: niente cicli (una collezione non può avere sé stessa come antenato). Un `ParentId`/`CollectionId` orfano al caricamento → trattato come radice.

## SessionConfig (esteso)

| Campo | Tipo | Note |
|---|---|---|
| … campi esistenti … | | invariati |
| `CollectionId` | Guid? | **nuovo**; null = radice |

- `FilePath` può contenere `{date:<fmt>}` (vedi DatePath). `SharePath` non può.
- `FullPath` (display) resta disponibile e mostra il percorso con i segnaposto non risolti; `ResolvePath(DateTime)` restituisce il percorso risolto.
- `Validate()` aggiunge: errori di `DatePath.Validate(FilePath)`; segnaposto nella share → "La data non è ammessa nella share."

## DatePath (logica statica)

- `Has(path)` → bool
- `Resolve(path, date)` → string (tutti i segnaposto)
- `Validate(path)` → lista di errori (formato vuoto, formato non valido, caratteri non ammessi nel risultato, componenti orarie)

## ActiveSession (esteso, runtime)

| Campo | Tipo | Note |
|---|---|---|
| `Date` | DateTime? | data seguita; null se il percorso non ha segnaposto |
| `FollowToday` | bool | `Date == DateTime.Today` all'avvio |
| `DisplayName` | string | `Name` oppure `Name · yyyy-MM-dd` |
| `Today` | Func<DateTime> | orologio iniettabile (default `() => DateTime.Today`), per testare il cambio giorno |

**Transizioni di stato**: nessuno stato nuovo. Il cambio giorno è un evento dentro `Running`/`Waiting`: marker `— nuovo giorno —`, tailer nuovo letto dall'inizio, poi i normali stati `Running` o `Waiting` (file non ancora creato).

## SessionTree (logica, in memoria)

Contiene `List<Collection> Collections` e `List<SessionConfig> Sessions`.

- `Children(parentId)` → collezioni e sessioni figlie, ordinate
- `CanMove(collectionId, newParentId)` → false se `newParentId` è la collezione stessa o una sua discendente
- `Move(item, newParentId)`
- `CountSessions(collectionId)` → ricorsivo, per la conferma di eliminazione
- `Delete(collectionId)` → rimuove la collezione, le sottocollezioni e le sessioni
- `UniqueName(parentId, name)` → `name`, `name (2)`, …
- `Export(collectionId?)` → `ExportFile` (null = radice: tutto); password azzerate
- `Import(ExportFile, targetParentId)` → aggiunge con ID nuovi e nomi univoci sulle radici importate

## Persistenza

- `sessions.json` v2: vedi [contracts/sessions-file.md](contracts/sessions-file.md)
- File di export: vedi [contracts/export-file.md](contracts/export-file.md)
- L'ordine delle schede non è persistito.
