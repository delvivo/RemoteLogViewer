# Contract: `sessions.json`: campo `workspace`

Aggiunta retro-compatibile al formato esistente (vedi `specs/001-remote-log-viewer-collections-tabs-datepath/contracts/sessions-file.md`). JSON camelCase.

```json
{
  "collections": [ ... ],
  "sessions": [ ... ],
  "credentials": [ ... ],
  "workspace": {
    "tabs": [
      { "sessionId": "3f2c…", "date": null, "followToday": false },
      { "sessionId": "9a10…", "date": "2026-06-11T00:00:00", "followToday": false },
      { "sessionId": "9a10…", "date": "2026-09-30T00:00:00", "followToday": true }
    ],
    "selected": 1,
    "sideBySide": false
  }
}
```

- `workspace` assente o `null` → nessuna scheda da ripristinare.
- La v1.0.0 ignora il campo e, al primo salvataggio, lo elimina: è accettabile.
- L'export (`*.rlv.json`) non contiene mai `workspace`.
