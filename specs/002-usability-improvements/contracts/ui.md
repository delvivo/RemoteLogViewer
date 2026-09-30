# Contract: UI

## Barra della sessione (`LogView`)

| Elemento | Comportamento |
|---|---|
| Checkbox `ERROR n` / `WARN n` / `INFO n` / `DEBUG n` | filtro per livello come oggi; `n` = voci nel buffer (FR-027/028) |
| `Contesto:` casella numerica (0–50) | righe prima/dopo ogni match del filtro testo; valori non validi → limitati a 0–50 |
| Pulsante `▲ Precedenti` | carica un blocco di righe precedenti (righe iniziali della sessione, 1000 se 0); disabilitato se `!CanLoadOlder` |
| Pulsante `Salva…` / **Ctrl+S** | salva le righe visibili; non fa nulla se la vista è vuota |

Righe di contesto: opacità ridotta. Separatore tra gruppi: riga `--` in stile marker.

## Finestra principale

| Elemento | Comportamento |
|---|---|
| Titolo scheda | `● Nome · data  [n]  ✕`: `[n]` è un badge rosso, visibile solo se `UnreadErrors > 0` |
| Taskbar | lampeggia se arriva un ERROR e la finestra non è attiva |
| Pulsante `Guida` (toolbar) / **F1** | apre la guida nel browser predefinito |
| Avvio | ripristina schede, ordine, selezione, Affianca |
| Chiusura | salva il workspace |

## Scorciatoie (elenco completo, da riportare nella guida)

| Tasto | Dove | Azione |
|---|---|---|
| F1 | ovunque | Guida |
| Invio | albero | avvia sessione / espande collezione |
| F2 | albero | modifica sessione / rinomina collezione |
| Canc | albero | elimina |
| Ctrl+F | sessione | vai alla ricerca |
| F3 / Shift+F3 | sessione | risultato successivo / precedente |
| Ctrl+C | righe | copia righe selezionate |
| Ctrl+S | sessione | salva righe visibili |
| Click centrale | titolo scheda | chiude la scheda |
