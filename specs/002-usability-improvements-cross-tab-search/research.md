# Research: Ricerca avanzata tra tutti i tab aperti

Nessun `NEEDS CLARIFICATION` residuo; qui le scelte tecniche.

## 1. Dove cercare

- **Decision**: istantanea di `LogView._all` (`ToArray()` sul thread UI, ~100k riferimenti per sessione), ricerca in `Task.Run` sulla copia.
- **Rationale**: `_all` è toccato solo dal thread UI (`Drain`); `LogLine.Text`/`Level` sono immutabili, quindi la copia è sicura da leggere in background. Nessun lock, nessuna interferenza con `ActiveSession`.
- **Alternatives**: cercare su `Session.Pending` (perde le righe già drenate); rileggere i file (lento, richiede SMB, fuori ambito).

## 2. Motore di corrispondenza

- **Decision**: sempre `Regex`. Testo semplice → `Regex.Escape`. Parola intera → `(?<!\w)…(?!\w)`. Maiuscole/minuscole → opzione `IgnoreCase` di default. Timeout 50 ms, scatto = nessuna corrispondenza (stesso comportamento del filtro di `LogView`).
- **Rationale**: un solo percorso, e `Match.Index/Length` danno gratis l'evidenziazione (FR-009). `\b` sbaglierebbe con testo che inizia/finisce per non-parola (es. `[ERR]`), i lookaround no.
- **Alternatives**: `string.Contains` per il semplice (niente span, due percorsi).

## 3. Timestamp (decisione lasciata aperta dal clarify)

- **Decision**: `LogTimestamp.TryParse(line, defaultDate)` riconosce a inizio riga (dopo spazi e un eventuale `[`):
  - `yyyy-MM-dd[T ]HH:mm:ss[,.fff]` (ISO)
  - `dd/MM/yyyy HH:mm:ss[,.fff]`
  - solo `HH:mm:ss[,.fff]` → data = `defaultDate` (data della sessione, altrimenti oggi)
  
  Ignora suffissi `Z`/offset (nessuna conversione, come da assunzione). Righe senza timestamp ereditano l'ultimo valore della stessa sessione, calcolato nella stessa passata della ricerca; senza precedente → escluse quando l'intervallo è impostato.
- **Rationale**: copre i formati tipici (log4net/Serilog/NLog/IIS) con una sola regex compilata; il calcolo per passata evita di aggiungere campi a `LogLine` (e memoria × 500k righe).
- **Alternatives**: parsare alla creazione di `LogLine` e salvarlo (costo memoria permanente per una funzione a richiesta); `DateTime.TryParse` generico (lento, ambiguo `dd/MM` vs `MM/dd`).

## 4. Livelli

- **Decision**: usa `LogLine.Level` (già ereditato dalle righe di continuazione). con il filtro livelli attivo (non tutti e 4 selezionati) le righe `LogLevel.None` (prima del primo livello rilevato) sono **escluse**, a differenza del filtro vista di `LogView` che non le nasconde mai.
- **Rationale**: in una ricerca "solo ERROR" l'utente non vuole righe senza livello; il filtro vista le tiene solo per non rompere la lettura.
- **Alternatives**: allineare a `LevelOk` (restituirebbe righe spurie).

## 5. Risultati e UI

- **Decision**: `ListBox` virtualizzata su lista piatta di righe (`ResultRow`: intestazione di gruppo oppure risultato) con `Pre/Match/Post` per l'evidenziazione. Limite 10.000 applicato in `LogSearch.Run` (`Truncated = true`).
- **Rationale**: `TreeView`/grouping di WPF virtualizzano male; con 10.000 righe una lista piatta resta fluida. Header non selezionabili.
- **Alternatives**: `ListCollectionView` con `GroupDescriptions` (virtualizzazione in gruppi fragile).

## 6. Salto alla riga

- **Decision**: `LogView.Reveal(LogLine)`: se `_all` non contiene la riga → `false` (messaggio "non più disponibile"); altrimenti azzera `_hiddenLevels` (spunta i quattro checkbox), `FilterBox.Text`, `Rebuild()`, `Follow` off, `Lines.SelectedItem`/`ScrollIntoView`; ritorna `filtersCleared` per l'avviso. `MainWindow` seleziona prima il `TabItem` (`Tag == LogView`); in Affianca nessuna selezione.
- **Rationale**: riuso di `Rebuild` e della selezione di `Find`; il riferimento a `LogLine` identifica la riga anche se gli indici sono slittati (prepend/trim).

## 7. Finestra

- **Decision**: `SearchWindow` non modale, `Owner = MainWindow`, una sola istanza (riattivata se già aperta), si chiude con la finestra principale. Parametri: `Func<IReadOnlyList<LogView>>` per le viste, `Action<LogView, LogLine>` per il salto.
- **Rationale**: stesse convenzioni di `CredentialsWindow`; il delegato evita il riferimento circolare a `MainWindow`.
