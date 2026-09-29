# Data Model: Remote Log Viewer

## SessionConfig (persistito)

| Campo | Tipo | Regole |
|-------|------|--------|
| Id | Guid | generato alla creazione |
| Name | string | obbligatorio, non vuoto |
| SharePath | string | obbligatorio, forma `\\server\share` |
| UserName | string | opzionale (vuoto = credenziali utente Windows corrente); ammesso `DOMINIO\utente` |
| ProtectedPassword | string? | base64 DPAPI CurrentUser; mai in chiaro |
| FilePath | string | obbligatorio; se relativo è combinato con SharePath, se UNC completo deve iniziare con SharePath |
| TailLines | int | default 1000, 0..100000 |
| Encoding | string | `auto` (default), `utf-8`, `utf-16`, `windows-1252` |

Percorso completo del file = `FullPath` (derivato).

## ActiveSession (runtime)

- Config: SessionConfig
- State: `Connecting | Running | Paused (follow off) | Reconnecting | Error | Stopped`
- Offset: long (posizione letta nel file), CreationTime del file letto
- Lines: buffer di LogLine (max 100.000)
- Filter: string? (testo o regex), Search: string?
- Follow: bool

Transizioni: `Stopped → Connecting → Running`; `Running ↔ Reconnecting` (IOException / riconnessione riuscita); `Connecting → Error` (credenziali/percorso errati: nessun retry automatico); `Running/Reconnecting/Error → Stopped` (stop utente). Follow è indipendente dallo stato di lettura.

## LogLine

- Text: string
- Level: `None | Debug | Info | Warn | Error`
- IsMarker: bool (righe di sistema tipo "— file ruotato —", "— disconnesso —")
