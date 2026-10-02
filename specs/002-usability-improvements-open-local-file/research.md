# Research: Apertura di un file log dal file system

Nessun `NEEDS CLARIFICATION` residuo; scelte tecniche.

## 1. Come rappresentare il file locale

- **Decision**: `SessionConfig` con `IsLocal = true`, creata da `SessionConfig.Local(path)`: `Name` = nome del file, `FilePath` = percorso assoluto, `TailLines = 1000`, `Encoding = "auto"`, `SharePath` vuoto. `FullPath` e `ResolvePath` restituiscono `FilePath` così com'è. Non entra mai in `SessionTree.Sessions`.
- **Rationale**: `ActiveSession`, `LogView`, ricerca avanzata, avvisi, Affianca e riordino lavorano già su `ActiveSession.Config`/`Path`/`DisplayName`; un caso particolare costa pochi `if`, un secondo tipo di scheda costerebbe un'altra via per ogni funzione (e per `Relayout`, `Workspace`, ricerca).
- **Alternatives**: classe `LocalSession` con interfaccia comune (astrazione per un solo caso aggiuntivo); creare una vera sessione salvata (inquina l'elenco, contro la spec).

## 2. Saltare SMB

- **Decision**: in `ActiveSession.Run` il blocco `Connect` e le due `Release` sono condizionati a `!Config.IsLocal`; `connected` resta `true` per i locali così il ramo `FileNotFound/DirectoryNotFound` porta a *In attesa del file…* e gli errori I/O a *Riconnessione* senza toccare la rete.
- **Rationale**: stato, retry e rotazione identici ai remoti (FR-011) senza duplicare il ciclo.
- **Alternatives**: ciclo separato per i locali (duplica la macchina a stati).

## 3. Controllo prima di aprire (FR-012)

- **Decision**: prima di creare la scheda, `MainWindow` prova `new FileStream(path, Open, Read, ReadWrite|Delete)`; se fallisce (non esiste, permessi, cartella) → `MessageBox` con il motivo e nessuna scheda. I file spariti *dopo* l'apertura seguono la macchina a stati (attesa/riconnessione).
- **Rationale**: un file locale sbagliato è quasi sempre un errore dell'utente; meglio dirlo subito che aprire una scheda in `Error`.
- **Alternatives**: aprire comunque la scheda e lasciare lo stato *Errore* (scheda inutile, spec vuole messaggio).

## 4. Dialogo e comando

- **Decision**: `ApplicationCommands.Open` (gesto `Ctrl+O` già associato dal framework) con `CommandBinding` sulla finestra e pulsante toolbar *Apri file…*; `OpenFileDialog { Multiselect = true }` con filtri *Log e testo (\*.log;\*.txt)* e *Tutti i file*.
- **Rationale**: stesso schema di `ApplicationCommands.Help`/`Save` già usati; nessun `KeyBinding` manuale.

## 5. Drag & drop

- **Decision**: `AllowDrop` sulla finestra e handler **`PreviewDragOver`/`PreviewDrop`** (tunneling) che agiscono solo se `e.Data.GetDataPresent(DataFormats.FileDrop)` e marcano `Handled`. Gli handler esistenti (schede: `DragOver` forza `Effects = None` per dati non-scheda; albero sessioni) non vedrebbero altrimenti il drop di un file e lo bloccherebbero.
- **Rationale**: i drag interni usano formati custom (`rlv-tab`, `rlv-tree-item`), mai `FileDrop`: nessuna interferenza (FR-005). Cartelle e non-file sono scartati con `File.Exists`.
- **Alternatives**: aggiungere il caso in ogni handler (tre punti da tenere allineati).

## 6. Duplicati e titoli

- **Decision**: stesso file = stesso `Path.GetFullPath` confrontato `OrdinalIgnoreCase` con i `Config.FilePath` locali già aperti → si seleziona la scheda esistente (FR-006). Titolo = nome del file; se esiste già una scheda locale con quel nome, il titolo diventa `cartella\nome`.
- **Rationale**: Windows ha percorsi case-insensitive; la cartella genitore basta nella pratica a distinguere.

## 7. Workspace

- **Decision**: `OpenTab.LocalPath` (stringa nullable, camelCase `localPath`). Una scheda locale salva `SessionId = Guid.Empty`, `LocalPath = path`. `Window_Loaded`: se `LocalPath` è valorizzato e `File.Exists` → `OpenLocal`, altrimenti salta in silenzio (FR-013); le altre schede restano come prima.
- **Rationale**: file v1-compatibile (un `sessions.json` nuovo letto da una versione vecchia ignora il campo e, avendo `SessionId` vuoto, salta la scheda). Nessun `Normalize` da toccare: `Workspace` non è normalizzato.
- **Alternatives**: lista separata di file recenti (più stato, nessun beneficio).
