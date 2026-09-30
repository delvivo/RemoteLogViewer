# Research: Miglioramenti di usabilità

## R1. Quando una scheda è "non visibile"

- **Decision**: `LogView.IsVisible`. Il `TabControl` tiene nel visual tree solo il contenuto della scheda selezionata; in Affianca tutte le `LogView` sono nel visual tree. Il contatore `UnreadErrors` cresce solo se `!IsVisible` e si azzera su `IsVisibleChanged → true`.
- **Rationale**: nessuna logica di selezione in `MainWindow`, e Affianca non ha casi speciali (FR-003 gratis). Con finestra minimizzata `IsVisible` resta `true`: niente badge, ma il lampeggio copre il caso.
- **Alternatives**: `Tabs.SelectionChanged` + flag Affianca in `MainWindow`: più codice, e `SelectionChanged` risale anche dai `ListBox` interni.

## R2. Lampeggio della taskbar

- **Decision**: P/Invoke `FlashWindowEx` con `FLASHW_ALL | FLASHW_TIMERNOFG` quando `!IsActive`. Si ferma da solo quando la finestra torna in primo piano.
- **Alternatives**: `TaskbarItemInfo.ProgressState`: non è un avviso standard e va azzerato a mano.

## R3. Voce vs riga (conteggi e badge)

- **Decision**: `LogLine.IsEntry = true` quando il livello è rilevato sulla riga stessa (`LogLevels.Detect(line, None) != None`). Le righe di continuazione ereditano il livello ma non sono voci.
- **Rationale**: uno stack trace = 1 errore (FR-005) senza cambiare il rilevamento livelli esistente.

## R4. Righe iniziali: niente avvisi

- **Decision**: `LogLine.IsHistory = true` per le righe da `ReadTail` (apertura e ri-lettura dopo "file scomparso"), eccetto la lettura dall'inizio dopo il cambio giorno (righe nuove per davvero). Le righe da `ReadBefore` arrivano da una coda separata e non avvisano mai. La rotazione passa da `Poll`, quindi avvisa.

## R5. Carica precedenti: offset esatto della prima riga

- **Decision**: riscrivere `ReadTail` come scansione **sui byte** all'indietro a blocchi da 256 KB, cercando i fine riga (`0A` per UTF-8/1252, `0A 00` per UTF-16LE, `00 0A` per UTF-16BE, allineati a 2 byte dal preambolo). Si ottengono `tailEnd` (inizio della riga parziale finale) e `_head` (inizio della prima riga restituita); si decodifica solo `[_head, tailEnd)`. `ReadBefore(n)` = stessa scansione da `_head`, decodifica di `[newHead, _head)`. `HasOlder = _head > preambolo`.
- **Rationale**: l'offset dei byte è esatto anche con caratteri non validi o CRLF, cosa impossibile ricodificando le stringhe. Le righe caricate sono contigue per costruzione (FR-018). I confini cadono sempre dopo un fine riga, quindi nessun carattere multibyte viene spezzato.
- **Rotazione**: `Poll` che rileva la rotazione mette `_head = preambolo` (il nuovo file è letto da 0, niente di precedente).
- **Alternatives**: mantenere l'algoritmo attuale e stimare l'offset con `GetByteCount`: sbagliato con byte non validi (U+FFFD) e CRLF rimossi.

## R6. Thread di "carica precedenti"

- **Decision**: la UI chiama `ActiveSession.RequestOlder(n)` (`Interlocked.Exchange` su un intero). Il loop, nell'iterazione successiva (≤ 500 ms), chiama `tailer.ReadBefore(n)` e accoda la lista in `ConcurrentQueue<List<LogLine>> Older`. `CanLoadOlder` (volatile) = `State == Running && tailer.HasOlder`, e un suo cambiamento solleva `StateChanged`.
- **Rationale**: `LogTailer` non è thread-safe; resta usato da un solo thread.

## R7. Contesto (grep -C) incrementale

- **Decision**: `ContextFilter` con stato `_lastShown` (indice nel buffer dell'ultima riga emessa) e `_after` (righe di contesto successive ancora da emettere). `Add(all, i)` restituisce le righe da aggiungere alla vista: per un match emette le righe `max(_lastShown+1, i-N)..i`, preceduto da `--` se c'è un buco; poi `_after = N`. Le checkbox di livello decidono se una riga del blocco viene mostrata, ma non cambiano il conteggio delle posizioni. Le righe separatrici (marker) sono sempre emesse e non toccano `_lastShown`. Con N = 0 o nessun filtro testo il comportamento è identico al predicato attuale.
- **Rationale**: `Drain` (append) e `Rebuild` (reset + replay) usano lo stesso codice. Trim e prepend fanno già `Rebuild`, quindi gli indici restano coerenti.
- **Distinzione visiva**: `LogLine.IsContext` (con notifica) → opacità ridotta. Separatore = `LogLine("--", None, isMarker: true)` creato solo nella vista, mai nel buffer.

## R8. Scroll stabile dopo il prepend

- **Decision**: il `ListBox` virtualizzato scorre per elementi (`CanContentScroll`), quindi `VerticalOffset` è un indice. Dopo `Rebuild` si fa `ScrollToVerticalOffset(old + (visibili dopo − visibili prima))` a priorità `Loaded`. Con Follow attivo si resta in fondo.

## R9. Contatori: dove mostrarli

- **Decision**: nel testo delle checkbox di livello (`ERROR 12`, `WARN 3`, …).
- **Rationale**: nessun nuovo controllo; il numero sta accanto al filtro che lo riguarda.
- **Aggiornamento**: incrementale in `Drain` e nel prepend; ricalcolo completo dopo trim; azzeramento con *Pulisci*.

## R10. Workspace

- **Decision**: `SessionTree.Workspace { Tabs: [OpenTab { SessionId, Date, FollowToday }], Selected, SideBySide }` serializzato in `sessions.json`. Viene scritto in `Window_Closing` e ripristinato su `Loaded` tramite `Open(config, date)`, che salta il `DateDialog`. La data viene ricalcolata così: il placeholder non c'è più → `null`; `FollowToday` → oggi; altrimenti `Date ?? oggi`.
- **Alternatives**: un file separato `workspace.json`: una seconda scrittura atomica per nessun vantaggio.

## R11. Guida HTML

- **Decision**: `src/RemoteLogViewer/Guida.html` come `EmbeddedResource`, con CSS inline e nessuna risorsa esterna (FR-031). All'apertura il testo `%VERSION%` viene sostituito con la versione dell'assembly, scritto in `%TEMP%\RemoteLogViewer\guida.html` e aperto con `Process.Start(UseShellExecute = true)`. Se fallisce, un messaggio mostra il percorso.
- **Rationale**: funziona anche con una publish single-file (niente file accanto all'exe da non perdere); la versione è sempre corretta senza aggiornarla a mano.
- **Alternatives**: `Content` copiato nell'output (versione da aggiornare a mano, file che si può perdere); finestra `WebBrowser` interna (più codice, motore IE).

## R12. Salvataggio

- **Decision**: `ApplicationCommands.Save` legato a Ctrl+S sulla `LogView` + pulsante *Salva*. `SaveFileDialog` con nome `<sessione>_<yyyyMMdd_HHmmss>.log` (caratteri invalidi → `_`), `File.WriteAllLines(path, _visible.Select(l => l.Text), UTF8 senza BOM)`. I separatori sono già righe `--`.
