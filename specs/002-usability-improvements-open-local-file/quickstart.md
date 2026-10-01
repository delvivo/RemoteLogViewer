# Quickstart: validare l'apertura di un file locale

## Test automatici

```powershell
dotnet test --filter "FullyQualifiedName~SessionConfigTests|FullyQualifiedName~ActiveSessionTests|FullyQualifiedName~SessionStoreTests"
dotnet test            # suite completa (incl. GuideTests e LocTests)
```

Attesi: tutti verdi. Coprono `SessionConfig.Local`, il tail di un file locale senza SMB (append, troncamento, file sparito → attesa) e il roundtrip di `OpenTab.LocalPath`.

## Prova manuale (SC-001…SC-005)

Prerequisiti: `taskkill //IM RemoteLogViewer.exe //F` se l'app è aperta; due file di prova, ad esempio

```powershell
"INFO a`nERROR b`n   at X()`nWARN c" | Set-Content $env:TEMP\a.log
"INFO uno" | Set-Content $env:TEMP\b.log
```

```powershell
dotnet run --project src/RemoteLogViewer
```

1. *Apri file…* (o `Ctrl+O`) → scegli `a.log`: nuova scheda `a.log`, tooltip = percorso completo, nessuna richiesta di credenziali (US1).
2. `Add-Content $env:TEMP\a.log "ERROR nuova"` → la riga compare entro ~1 s con Follow attivo.
3. Riapri `a.log` → si seleziona la scheda esistente, nessun duplicato.
4. *Apri file…* con selezione multipla (`a.log` + `b.log`) → una scheda per file.
5. Annulla il dialogo → nessun effetto.
6. Trascina `b.log` da Esplora risorse sulla finestra (anche sopra una scheda e sopra l'elenco sessioni) → apre/seleziona la scheda (US2). Trascina una cartella → ignorata con messaggio. Riordina le schede e sposta una sessione nell'elenco → funzionano come prima.
7. Nella scheda locale: filtro per livello, regex, contesto, `Ctrl+F`, *▲ Precedenti*, *Salva…*, `Ctrl+Maiusc+F` (compare anche tra i risultati) (US3).
8. Con una scheda locale non visibile, `Add-Content a.log "ERROR x"` → badge sul titolo; con l'app in background, lampeggia la barra (US3).
9. Cancella il file aperto → *In attesa del file…*; ricrealo → riprende (US3).
10. File inesistente/senza permessi (es. via dialogo digitando un nome inesistente, o un file bloccato in esclusiva) → messaggio, nessuna scheda.
11. Chiudi e riavvia: la scheda locale torna insieme a quelle remote; elimina `b.log` prima del riavvio → la scheda viene saltata senza avvisi (US4).
12. Cambia lingua: titolo del dialogo e messaggi si aggiornano.

Pulizia: la prova scrive il vero `sessions.json` (workspace); elimina i file temporanei e chiudi le schede prima di uscire (CLAUDE.md, Gotchas).
