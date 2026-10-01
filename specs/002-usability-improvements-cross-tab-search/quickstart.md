# Quickstart: validare la ricerca avanzata

## Test automatici

```powershell
dotnet test --filter "FullyQualifiedName~LogSearchTests|FullyQualifiedName~LogTimestampTests"
dotnet test            # suite completa (incl. GuideTests: stessi id sezione it/en)
```

Attesi: tutti verdi. Coprono testo/regex, maiuscole/minuscole, parola intera, livelli (con righe senza livello), intervallo con timestamp ISO / `dd/MM/yyyy` / solo orario, ereditarietà del timestamp, limite 10.000, regex non valida.

## Prova manuale (spec: SC-001…SC-005)

Prerequisiti: `taskkill //IM RemoteLogViewer.exe //F` se l'app è già aperta; due file di log locali o su share con righe `ERROR`/`WARN` e la stessa parola (es. `timeout`).

```powershell
dotnet run --project src/RemoteLogViewer
```

1. Apri due sessioni sui due file → `Ctrl+Maiusc+F`: si apre la finestra, restano usabili le schede.
2. Cerca `timeout` → due gruppi con conteggi, corrispondenza evidenziata (US1).
3. Doppio click su un risultato della scheda non attiva → scheda selezionata, riga evidenziata. Prima nascondi ERROR e metti un filtro testo nella scheda: dopo il salto i filtri sono azzerati e compare "filtri rimossi" (US2).
4. `Maiuscole/minuscole` + `Timeout` non trova `timeout`; `Parola intera` + `err` non trova `error` (US3).
5. Solo ERROR; poi intervallo orario: solo righe nell'intervallo, estremi inclusi (US3).
6. Regex `(` → errore, risultati precedenti intatti.
7. `Esporta…` → il file contiene le stesse righe, raggruppate con intestazione `# nome (N)` (US4).
8. Cambia lingua dalla toolbar: i testi della finestra si aggiornano.
9. Con un buffer grosso (apri sessione con `TailLines` alto): UI reattiva durante la ricerca, `Annulla` funziona.

Pulizia: i test manuali scrivono il vero `sessions.json` (vedi CLAUDE.md, Gotchas); ripristina le sessioni create.
