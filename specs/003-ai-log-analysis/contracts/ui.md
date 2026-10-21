# UI Contract: Analisi IA

## Ingresso

- Toolbar `MainWindow`: pulsante **Analisi IA…** (tooltip "Analizza i log con uno strumento IA (Ctrl+Maiusc+I)").
- Scorciatoia **Ctrl+Maiusc+I** → `MainWindow.Ai` (`RoutedCommand`).
- Finestra `AiWindow` singleton non modale (owner = main window): se già aperta viene ripristinata/attivata. Chiusura della main window → chiusura → processo terminato.

## Layout `AiWindow` (ridimensionabile)

```
┌ Schede ───────────────┐┌ Strumento: [Claude Code ▾]  Comando: [claude -p ...      ] [Ripristina] ┐
│ ☑ app · 2026-10-21    ││ Prompt: [Report errori ▾]                                              │
│ ☐ db                  ││ ┌──────────────────────────────────────────────────────────────────┐   │
│ ☑ web                 ││ │ testo prompt (multilinea, modificabile)                          │   │
│                       ││ └──────────────────────────────────────────────────────────────────┘   │
│                       ││ ⚠ I log selezionati vengono inviati allo strumento IA scelto…          │
│                       ││ [Avvia] [Annulla]   stato · 00:12 · 1.234 righe escluse                │
│                       ││ ┌ risposta (TextBox sola lettura, Consolas, scroll, selezionabile) ┐   │
│                       ││ └──────────────────────────────────────────────────────────────────┘   │
│                       ││ [Copia] [Apri formattato] [Esporta…]                                   │
└───────────────────────┘└────────────────────────────────────────────────────────────────────────┘
```

## Comportamento

| Elemento | Regola |
|---|---|
| Elenco schede | `CheckBox` per ogni scheda aperta in ordine; aggiornato su `Activated` e prima dell'avvio; scheda attiva preselezionata alla prima apertura; selezioni mantenute per oggetto `LogView`. |
| Strumento | `ComboBox` di `AiTool.All`; cambio → mostra `CommandFor(id)`; salvato. |
| Comando | `TextBox`; modifica salvata per lo strumento corrente; **Ripristina** → predefinito. |
| Prompt preset | `ComboBox` (Report errori, Riepilogo); selezione → sostituisce il testo del prompt. |
| Avvia | abilitato se: nessuna analisi in corso, ≥1 scheda spuntata, prompt e comando non vuoti. Primo uso dello strumento → `MessageBox` Sì/No di consenso; No → non parte. |
| Annulla | abilitato solo durante l'analisi; termina l'albero di processi. |
| Stato | `In corso… mm:ss` / `Completato in mm:ss` / `Annullato` / `Errore (codice N)` / `Nessuna risposta`; + "N righe escluse per limite 1 MB" se `Dropped > 0`. Localizzato live (`L.Changed`). |
| Risposta | `TextBox` sola lettura, accodamento a blocchi dal timer UI; testo già ricevuto conservato su annulla/errore; in errore viene accodato stderr. |
| Copia / Apri formattato / Esporta… | abilitati se risposta non vuota e nessuna analisi in corso. Esporta: `SaveFileDialog` filtri `Markdown (*.md)` e `HTML (*.html)`, UTF-8 senza BOM. |

## Testo inviato (stdin)

```
<prompt>

<se Dropped>0: "(N righe più vecchie omesse per limite di dimensione)">
=== <nome scheda> (<percorso>) ===
<righe>

=== ... ===
```

## Localizzazione

Ogni stringa nuova in `L.cs` (`En`); XAML con `{local:T '…'}`; testo da codice aggiornato su `L.Changed` (sottoscrizione rimossa su `Closed`). I testi dei prompt preset seguono la lingua attiva al momento della selezione.
