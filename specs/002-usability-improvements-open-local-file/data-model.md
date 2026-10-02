# Data Model: Apertura di un file locale

## SessionConfig (esteso)

| Campo | Tipo | Note |
|---|---|---|
| `IsLocal` | bool (default false) | true = file del file system, nessun SMB |
| `FilePath` | string | per i locali: percorso assoluto (`C:\log\app.log`, UNC o unità mappata) |
| `Name` | string | per i locali: nome del file, o `cartella\nome` se già in uso da un'altra scheda locale |
| `TailLines` | int | per i locali: 1000 fisso |
| `Encoding` | string | per i locali: `auto` fisso |
| `SharePath`, `UserName`, `ProtectedPassword`, `CredentialId`, `CollectionId`, `CurrentUndated` | — | non usati dai locali, lasciati ai default |

`SessionConfig.Local(string path)` → config locale (percorso normalizzato con `Path.GetFullPath`). `FullPath`/`ResolvePath(...)` → `FilePath` se `IsLocal`. `Validate()` non si applica (non passa dalla finestra di modifica) e una config locale **non entra mai** in `SessionTree.Sessions`, né negli export.

## OpenTab (esteso)

| Campo | Tipo | Note |
|---|---|---|
| `SessionId` | Guid | `Guid.Empty` per i locali |
| `LocalPath` | string? | percorso del file locale; null per le sessioni salvate |
| `Date`, `FollowToday` | — | invariati, non usati dai locali |

JSON: `localPath` (camelCase), assente per le sessioni remote → file v1-compatibile.

## Regole

- Identità: due schede locali sono "lo stesso file" se `Path.GetFullPath` coincide, confronto `OrdinalIgnoreCase`.
- Un file locale non ha stato proprio oltre a quello di `ActiveSession` (*Attiva*, *In attesa del file…*, *Riconnessione…*, *Ferma*, *Errore*).
- Riapertura all'avvio: `LocalPath` valorizzato + `File.Exists` → riaperto; altrimenti saltato in silenzio.

## Transizioni

Invariate: apertura → `Connecting` → `Running`; file assente → `Waiting`; errore I/O dopo una lettura riuscita → `Reconnecting` (5 s); prima lettura fallita → `Error`. Per i locali non esiste la fase di connessione di rete.
