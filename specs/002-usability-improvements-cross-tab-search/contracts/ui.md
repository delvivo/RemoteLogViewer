# UI Contract: Ricerca avanzata

## Accesso

- Pulsante toolbar **Cerca…** (en: *Search…*) in `MainWindow`, tooltip con la scorciatoia.
- `Ctrl+Maiusc+F` sulla finestra principale. Se la finestra esiste già viene riportata in primo piano.
- Tabella scorciatoie delle guide e dei README aggiornata.

## Finestra `SearchWindow`

Non modale, ridimensionabile, `Owner = MainWindow`, una sola istanza.

| Controllo | Comportamento |
|---|---|
| Testo | Invio = Cerca. Vuoto + filtri → elenca per filtri |
| ☐ Regex | bordo rosso/messaggio se non valida |
| ☐ Maiuscole/minuscole, ☐ Parola intera | default off |
| ☑ ERROR ☑ WARN ☑ INFO ☑ DEBUG | default tutti; con filtro parziale le righe senza livello sono escluse |
| Da / A | `DatePicker` + ora `HH:mm[:ss]`, facoltativi |
| **Cerca** / **Annulla** | Annulla attivo solo durante la ricerca |
| Lista risultati | intestazione per sessione `Nome · N`, sotto le righe con corrispondenza evidenziata e livello; doppio click (o Invio) su una riga = salto; intestazioni non attivabili |
| Stato | "N risultati in M sessioni" · "Nessun risultato" · "Nessuna sessione aperta" · "Risultati limitati a 10.000: restringi la ricerca" · errore regex |
| **Esporta…** | disabilitato senza risultati; `SaveFileDialog` (`.log`/`.txt`), UTF-8 senza BOM |

Testi in it (chiave) + `En` in `L.cs`; cambio lingua dal vivo (`L.Bind` / `L.Changed`, unsubscribe in `Closed`).

## Formato file esportato

```text
# <nome sessione 1> (N)
<riga>
<riga>

# <nome sessione 2> (M)
<riga>
```

Solo i risultati mostrati (max 10.000), ordine come in lista.

## Salto (doppio click)

1. Se la riga non è più nel buffer: messaggio "riga non più disponibile", nessun altro effetto.
2. Seleziona la scheda (in Affianca nessuna selezione, la vista viene solo evidenziata).
3. Se la riga è nascosta dai filtri della scheda: azzera livelli e testo, avviso "filtri rimossi" (non modale, es. barra di stato della scheda).
4. Follow off, riga selezionata e in vista.
