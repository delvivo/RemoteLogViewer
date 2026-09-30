# Contract: UI

## Finestra principale

- Pannello sinistro: elenco sessioni salvate. Azioni: Nuova, Modifica, Duplica, Elimina, Avvia (doppio click).
- Area centrale: `TabControl`, una scheda per sessione attiva; intestazione = nome + indicatore stato (colore) + ✕ chiudi.
- Toolbar: toggle **Affianca** (tutte le schede aperte in griglia).

## Vista sessione (LogView)

- Barra: stato testuale, **Follow** (toggle), **Stop/Avvia**, **Pulisci**, campo **Filtro** (checkbox Regex), campo **Cerca** (▲▼ / F3, Shift+F3).
- Lista righe: monospace, colori — Error rosso, Warn arancio, Info default, Debug grigio; marker in corsivo.
- Scroll verso l'alto → Follow off; Follow on → salto in fondo.
- Ctrl+C copia righe selezionate (selezione multipla).

## Form sessione

- Campi: Nome, Share (`\\server\share`), Utente, Password (PasswordBox; vuota in modifica = invariata), File, Righe iniziali, Encoding.
- **Test connessione**: connette la share e verifica esistenza file; mostra esito. **Salva** valida i campi obbligatori.
