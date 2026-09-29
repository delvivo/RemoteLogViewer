# Contract: UI

## Sidebar "Sessioni salvate" (TreeView)

- Nodi collezione: icona cartella + nome, espandibili. Nodi sessione: nome in grassetto + percorso grigio (template attuale; il percorso mostra `{date:…}` non risolto).
- Ordinamento: collezioni per nome, poi sessioni per nome.
- Lo stato espanso sopravvive alla ricostruzione dell'albero (set di ID in memoria, non persistito).
- **Pulsanti** (in basso): Avvia · Nuova · Modifica · Duplica · Elimina · Collezione (nuova).
- **Menu contestuale**:
  - su una sessione: Avvia, Modifica, Duplica, Sposta in…, Elimina
  - su una collezione: Nuova sessione qui, Nuova sottocollezione, Rinomina, Sposta in…, Esporta…, Importa qui…, Elimina
  - su spazio vuoto: Nuova sessione, Nuova collezione, Esporta tutto…, Importa…
- **Sposta in…**: sottomenu "(radice)" + tutte le collezioni come percorsi `PROD › API`. Le destinazioni non valide (la collezione stessa e le discendenti) sono disabilitate.
- **Drag & drop**: sessione o collezione su collezione = sposta dentro; su spazio vuoto = sposta alla radice. Drop non valido → cursore "vietato", nessuna azione.
- **Tastiera**: Invio = Avvia (sessione) o espandi/comprimi (collezione); Canc = Elimina; F2 = Modifica/Rinomina.
- **Nuova/Duplica**: il nuovo elemento va nella collezione selezionata (o in quella della sessione selezionata).
- **Elimina collezione**: `Eliminare la collezione "X" con N sessioni e M sottocollezioni?` (Sì/No). Se vuota: `Eliminare la collezione "X"?`
- **Errori**: nome vuoto → "Nome obbligatorio."; nome duplicato tra sorelle → "Esiste già una collezione con questo nome."; import fallito → MessageBox con il messaggio del contract.

## Editor sessione

- Sotto "File": riga grigia `Anteprima: <percorso risolto con oggi>` quando c'è un segnaposto, più il suggerimento `Usa {date:yyyy_MM_dd} per una data variabile`.
- Gli errori del segnaposto rientrano nella validazione esistente (blocca il salvataggio).

## Dialog data (DateDialog)

- Titolo: `Avvia "<nome sessione>"`. Contenuto: `DatePicker` (default oggi), `Anteprima: <percorso risolto>`, OK/Annulla. Invio = OK, Esc = Annulla.
- Annulla → la sessione non si avvia.

## Schede

- Titolo scheda: `Nome` o `Nome · 2026-06-11` (data seguita, aggiornata al cambio giorno). Tooltip: percorso risolto.
- Trascinare una scheda su un'altra la sposta in quella posizione e la lascia selezionata.
- La vista "Affianca" usa l'ordine delle schede. Le celle affiancate non si trascinano.
- Marker nella vista al cambio giorno: `— nuovo giorno: 2026-10-01 —`.
