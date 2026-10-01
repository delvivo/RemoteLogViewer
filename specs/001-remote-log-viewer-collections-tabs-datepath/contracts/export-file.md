# Contract: file di export/import

Estensione suggerita: `.rlv.json` (filtro dei dialog: `Collezioni Remote Log Viewer (*.rlv.json)|*.rlv.json|JSON (*.json)|*.json`). UTF-8, JSON indentato, camelCase.

```json
{
  "format": "remote-log-viewer-collection",
  "version": 1,
  "collections": [
    { "id": "a1…", "name": "PROD", "parentId": null },
    { "id": "b2…", "name": "API",  "parentId": "a1…" }
  ],
  "sessions": [
    {
      "id": "3f1c…", "name": "FOLDER standard", "collectionId": "b2…",
      "sharePath": "\\\\server\\E$", "userName": "DOMAIN\\me", "protectedPassword": null,
      "filePath": "…\\{date:yyyy_MM_dd}\\log.log", "tailLines": 1000, "encoding": "auto"
    }
  ]
}
```

## Export

- Collezione selezionata → quella collezione (con `parentId: null` nel file), tutte le discendenti e le loro sessioni.
- Radice / nessuna selezione → tutte le collezioni e tutte le sessioni (radici con `parentId`/`collectionId` null).
- `protectedPassword` è **sempre** `null`: nessuna password, né in chiaro né cifrata.

## Import (tutto o niente)

1. Parsing del JSON. `format` diverso o `version` > 1 → errore "File non riconosciuto" / "Versione del file non supportata". Campi sconosciuti ignorati.
2. Validazione: ID duplicati, riferimenti orfani o cicli → errore "File non valido". Nessuna modifica all'albero.
3. Ogni `id` viene rimappato a un nuovo Guid (e con esso `parentId`/`collectionId`).
4. Gli elementi radice del file (collezioni e sessioni con parent null) vengono agganciati alla collezione di destinazione (o alla radice). In caso di conflitto di nome con un fratello esistente il nome diventa `Nome (2)`, `Nome (3)`, …
5. Le sessioni importate non hanno password: all'avvio `SmbConnection` usa le credenziali correnti/il mapping esistente o fallisce con "Accesso negato"; l'utente la inserisce con Modifica.
6. Salvataggio in `sessions.json`, poi espansione e selezione del primo elemento importato.
