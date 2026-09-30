# Quickstart / Validation: Remote Log Viewer

## Prerequisiti

- Windows 10/11, .NET 10 SDK
- Una share raggiungibile. Per test locali: condividere una cartella (es. `C:\logtest` come `logtest`) → `\\localhost\logtest`, oppure usare `\\localhost\C$` con utente amministratore.

## Build, test, avvio

```powershell
dotnet build
dotnet test
dotnet run --project src/RemoteLogViewer
```

## Scenari

1. **Tail base (US1)**: crea `C:\logtest\app.log` con 2000 righe. Nuova sessione: share `\\localhost\logtest`, file `app.log`, righe 1000 → Avvia. Atteso: righe 1001-2000.
   Poi `1..50 | % { Add-Content C:\logtest\app.log "$(Get-Date -f o) INFO riga $_"; Start-Sleep -m 100 }` → righe visibili entro 2 s.
2. **Errori (US1)**: password errata → "Credenziali non valide"; file inesistente → attesa del file / messaggio; server `\\nonesiste\x` → "Server non raggiungibile".
3. **Persistenza (US2)**: salva 2 sessioni, chiudi e riapri → presenti, avviabili senza password. `Select-String -Path $env:APPDATA\RemoteLogViewer\sessions.json -Pattern <password>` → nessun risultato.
4. **Parallelo (US3)**: 3 sessioni su 3 file, **Affianca**, scrivi in tutti → ogni vista si aggiorna. Una sessione con share errata non blocca le altre.
5. **Vista (US4)**: `Add-Content app.log "ERROR boom"`, `"WARN hmm"` → colori; filtro `ERROR` → solo errori, nuove righe filtrate; scroll su → follow si ferma; Follow → riprende.
6. **Rotazione**: `Clear-Content C:\logtest\app.log; Add-Content C:\logtest\app.log "INFO nuovo"` → marker "file troncato/ruotato" + "INFO nuovo".
7. **Unit test**: `dotnet test` verde (tailer: coda N righe, append, riga parziale, troncamento; livelli; store round-trip DPAPI).
