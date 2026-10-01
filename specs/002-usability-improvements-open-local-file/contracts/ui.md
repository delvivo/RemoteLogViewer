# UI Contract: Apertura di un file locale

## Accesso

- Pulsante toolbar **Apri file…** (en: *Open file…*), tooltip con `Ctrl+O`.
- `Ctrl+O` sulla finestra principale (`ApplicationCommands.Open`).
- Drag & drop di file da Esplora risorse sulla finestra (ovunque: schede, elenco sessioni, area vuota).
- Tabella scorciatoie di guide e README aggiornata.

## Dialogo di selezione

`OpenFileDialog`, selezione multipla. Filtri: *Log e testo (\*.log;\*.txt)* (default) e *Tutti i file (\*.\*)*. Annullare = nessun effetto. Titolo: *Apri file di log*.

## Comportamento all'apertura (per ogni file, nell'ordine scelto o trascinato)

| Caso | Esito |
|---|---|
| Non è un file (cartella, elemento non file) | ignorato; se nessun elemento è utilizzabile: messaggio *Nessun file valido* |
| Già aperto in una scheda locale (percorso uguale, maiuscole ignorate) | si seleziona la scheda esistente |
| Non esiste / non leggibile / permessi | messaggio *Impossibile aprire «{0}»: {1}*; nessuna scheda; gli altri file proseguono |
| OK | nuova scheda, selezionata (l'ultima aperta) |

Nessuna richiesta di credenziali, data o conferma.

## Scheda su file locale

- Titolo: nome del file (`cartella\nome` se un'altra scheda locale ha lo stesso nome); suggerimento: percorso completo; pallino di stato come le altre.
- Ultime 1000 righe, poi Follow; barra della sessione, filtri, contesto, ricerca, *▲ Precedenti*, *Salva…*, *Pulisci*, *A capo*, avvisi di errore (badge/lampeggio), Affianca, riordino per trascinamento, chiusura (✕/tasto centrale): identici alle sessioni remote.
- Inclusa nella ricerca avanzata (nome = titolo della scheda).
- Stati: *Attiva*, *In attesa del file…* (file sparito), *Riconnessione…* (errore I/O), *Ferma*.

## Riapertura all'avvio

Le schede locali sono ricordate nel workspace e riaperte se il file esiste ancora, saltate in silenzio altrimenti.

## Drag & drop: compatibilità

I drag interni (riordino schede, spostamento nell'elenco sessioni) usano formati propri e non cambiano: l'handler dei file reagisce solo a `FileDrop`.

Testi in it (chiave) + `En` in `L.cs`; cambio lingua dal vivo.
