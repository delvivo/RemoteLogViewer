# Contract: sessions.json

Percorso: `%APPDATA%\RemoteLogViewer\sessions.json`. UTF-8, scritto atomicamente (file temporaneo + replace).

```json
{
  "sessions": [
    {
      "id": "3f1c…",
      "name": "PROD – API",
      "sharePath": "\\\\nts11050\\E$",
      "userName": "DOMAIN\\myusername",
      "protectedPassword": "AQAAANCMnd8BFdERjHoAwE/Cl+sBAAAA…",
      "filePath": "logs\\api\\app.log",
      "tailLines": 1000,
      "encoding": "auto"
    }
  ]
}
```

- Campi mancanti → default del [data-model](../data-model.md).
- File assente → elenco vuoto. File corrotto → rinominato `sessions.json.bak`, elenco vuoto, avviso all'utente.
- `protectedPassword` non decifrabile (altro utente/macchina) → sessione caricata senza password, l'utente deve reinserirla.
