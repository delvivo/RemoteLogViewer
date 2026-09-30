# Research: Remote Log Viewer

## 1. Autenticazione alla share (equivalente `New-SmbMapping`)

- **Decision**: P/Invoke `mpr.dll!WNetAddConnection2W` con `NETRESOURCE{ dwType=RESOURCETYPE_DISK, lpLocalName=null, lpRemoteName=\\server\share }`, user/password; rilascio con `WNetCancelConnection2W(remote, 0, true)`.
- **Rationale**: stessa API che usa `net use` / `New-SmbMapping` senza lettera; dopo la connessione i path UNC sono leggibili con normale `FileStream`. Nessuna dipendenza.
- **Errori da mappare**: 5 accesso negato, 53/67 rete/nome non trovato, 86/1326 credenziali errate, 1219 connessione esistente con credenziali diverse (messaggio dedicato: "esiste già una connessione a questo server con altre credenziali"), 85 già connesso (trattato come OK).
- **Condivisione**: connessione per share con reference count in-process; cancellata solo quando l'ultima sessione dell'app su quella share si chiude, e solo se è stata l'app ad aprirla (FR-017).
- **Alternatives**: lanciare PowerShell `New-SmbMapping` (lento, dipende da modulo SMB); impersonation `LogonUser` + `LOGON32_LOGON_NEW_CREDENTIALS` (più complesso, per-thread).

## 2. Archiviazione password

- **Decision**: `ProtectedData.Protect(bytes, entropy, DataProtectionScope.CurrentUser)` → base64 nel JSON.
- **Rationale**: cifratura legata all'utente Windows, nessuna gestione chiavi. Pacchetto Microsoft `System.Security.Cryptography.ProtectedData`.
- **Alternatives**: Windows Credential Manager (`CredWrite`, più P/Invoke, limiti di dimensione); nessuna persistenza password (contrario a US2).

## 3. Tail del file

- **Decision**: `FileStream(path, Open, Read, FileShare.ReadWrite | FileShare.Delete)`. Avvio: seek a fine file, leggi a ritroso a blocchi da 64 KB finché trovate N newline (o inizio file). Poi polling ogni 500 ms della lunghezza: se cresce leggi il delta; se diminuisce → troncamento/rotazione, riparti da 0 e segnala. Riga incompleta finale tenuta in buffer finché arriva `\n`.
- **Rotazione con rename**: ogni polling riapre il file (handle breve) confrontando anche la data di creazione; se cambia → nuovo file, riparti da 0.
- **Rationale**: `FileSystemWatcher` su share SMB è inaffidabile; polling è semplice e prevedibile. Handle breve non blocca la rotazione lato server.
- **Encoding**: rilevamento BOM (UTF-8/UTF-16 LE/BE), altrimenti UTF-8; override per sessione (UTF-8, UTF-16, Windows-1252 tramite `CodePagesEncodingProvider`, in-box). Decoder stateful per non spezzare caratteri multibyte fra letture.
- **Alternatives**: `FileSystemWatcher` (eventi persi su SMB), handle tenuto aperto (può bloccare rotazione).

## 4. Riconnessione

- **Decision**: su `IOException` → stato Disconnesso, retry ogni 5 s (riconnette share + riapre file), riprende dall'offset salvato; se il file è più corto → rotazione.
- **Rationale**: soddisfa FR-015/SC-005 senza duplicati.

## 5. UI e prestazioni

- **Decision**: WPF code-behind (niente framework MVVM). Lista righe = `ListBox` virtualizzato (`VirtualizingStackPanel.VirtualizationMode=Recycling`) su `ObservableCollection<LogLine>` delle righe visibili. Il tailer lavora su thread di background e accoda righe; un `DispatcherTimer` (200 ms) le aggiunge in batch. Filtro: la lista visibile viene ricostruita dal buffer completo al cambio filtro; le nuove righe vengono filtrate in ingresso. Colori per livello via `DataTrigger`. Ricerca: evidenzia righe che contengono il testo, F3/Shift+F3 selezionano successiva/precedente.
- **Multi-sessione**: `TabControl`; toggle "Affianca" mostra tutte le sessioni aperte in una griglia (`UniformGrid`).
- **Buffer**: oltre 100.000 righe si rimuovono le più vecchie (in blocchi da 10.000 per evitare O(n) per riga).
- **Alternatives**: AvalonEdit (dipendenza extra, non necessario per righe colorate); TextBox unico (lento con molte righe, niente colore per riga).

## 6. Rilevamento livello

- **Decision**: regex case-insensitive con word boundary sulla riga: `FATAL|CRITICAL|ERROR|ERR` → Error; `WARN|WARNING` → Warn; `INFO` → Info; `DEBUG|TRACE|VERBOSE` → Debug; primo match a sinistra vince. Righe senza livello (es. stack trace) ereditano il livello della riga precedente.
- **Rationale**: copre log4net/NLog/Serilog/IIS senza configurazione.
