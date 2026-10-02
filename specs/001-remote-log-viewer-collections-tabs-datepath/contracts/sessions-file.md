# Contract: sessions.json (v2)

Percorso invariato: `%APPDATA%\RemoteLogViewer\sessions.json`. Scrittura atomica e gestione del file corrotto invariate (vedi [contract v1](../../001-remote-log-viewer/contracts/sessions-file.md)).

```json
{
  "collections": [
    { "id": "a1…", "name": "PROD", "parentId": null },
    { "id": "b2…", "name": "API",  "parentId": "a1…" }
  ],
  "sessions": [
    {
      "id": "3f1c…",
      "name": "FOLDER standard",
      "collectionId": "b2…",
      "sharePath": "\\\\server\\E$",
      "userName": "DOMAIN\\me",
      "protectedPassword": "AQAAANCM…",
      "filePath": "FOLDER\\APP_NAME\\log\\standard_logs\\{date:yyyy_MM_dd}\\log.log",
      "tailLines": 1000,
      "encoding": "auto"
    }
  ]
}
```

- **Migrazione da v1**: `collections` assente → lista vuota; `collectionId` assente → radice. Nessuna riscrittura forzata: il file diventa v2 al primo salvataggio.
- `collectionId` o `parentId` che puntano a un ID inesistente → elemento alla radice.
- Un ciclo nei `parentId` (file editato a mano) → la collezione che chiude il ciclo va alla radice.
- La versione precedente dell'app ignora `collections`/`collectionId`: le sessioni restano utilizzabili (flat).
