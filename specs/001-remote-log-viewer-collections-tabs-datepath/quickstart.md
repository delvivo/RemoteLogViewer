# Quickstart / validazione: Collezioni, riordino schede e percorsi con data

## Prerequisiti

- Build ok: `dotnet build`, test ok: `dotnet test`.
- Share raggiungibile. In locale: `\\localhost\C$` come utente corrente.
- **Backup** di `%APPDATA%\RemoteLogViewer\sessions.json` prima di iniziare. Il test manuale usa il file reale: ripristinalo alla fine.

## Setup file di prova

```powershell
$d = Get-Date; $y = $d.AddDays(-1)
New-Item -ItemType Directory -Force "C:\temp\rlv\$($d.ToString('yyyy_MM_dd'))", "C:\temp\rlv\$($y.ToString('yyyy_MM_dd'))" | Out-Null
"oggi 1" | Set-Content "C:\temp\rlv\$($d.ToString('yyyy_MM_dd'))\log.log"
"ieri 1" | Set-Content "C:\temp\rlv\$($y.ToString('yyyy_MM_dd'))\log.log"
"file oggi" | Set-Content "C:\temp\rlv\log_$($d.ToString('yyyy-MM-dd')).log"
```

## Scenari

| # | Passi | Atteso | Spec |
|---|---|---|---|
| 1 | Nuova sessione: share `\\localhost\C$`, file `temp\rlv\{date:yyyy_MM_dd}\log.log` | L'anteprima mostra la cartella di oggi; salvataggio ok | US1, FR-001/002 |
| 2 | File `temp\rlv\{date:}\log.log` | Errore di validazione, non salva | Edge |
| 3 | Avvia la sessione 1, scegli ieri | Mostra `ieri 1`; titolo `Nome · <ieri>` | US1-1/2/6 |
| 4 | Avvia di nuovo, scegli oggi | Seconda scheda con `oggi 1` | US1 |
| 5 | Sessione con file `temp\rlv\log_{date:yyyy-MM-dd}.log` | Mostra `file oggi` | US1-3 |
| 6 | Scegli una data futura | Stato "In attesa del file…" | US1-5 |
| 7 | Sessione senza segnaposto | Nessun dialog data | US1-4 |
| 8 | Cambio giorno: unit test `ActiveSession` con orologio iniettabile, oppure data di sistema portata a 23:59 | Marker `— nuovo giorno —`, titolo aggiornato | US1-7/8 |
| 9 | Crea `PROD` > `API`, trascina 2 sessioni in `API`, riavvia l'app | Struttura conservata, sessioni avviabili | US2-1/2/3 |
| 10 | Trascina `PROD` dentro `API` | Vietato | US2-5 |
| 11 | Elimina `PROD` | Conferma con il conteggio; dopo "Sì" sparisce tutto | US2-4 |
| 12 | Avvia con un `sessions.json` v1 (dal backup) | Tutte le sessioni alla radice | US2-6, SC-004 |
| 13 | Esporta `PROD`, apri il file | Nessuna password (`protectedPassword: null`) | US4-1/7 |
| 14 | Importa il file 2 volte alla radice | `PROD` e `PROD (2)`, indipendenti | US4-3/4/5 |
| 15 | Esporta tutto dalla radice, importa in una collezione vuota | Albero completo sotto la destinazione | US4-2 |
| 16 | Importa un file `{}` o testo | Errore, albero invariato | US4-6 |
| 17 | 3 schede aperte, trascina la terza in prima posizione, poi "Affianca" | Nuovo ordine in entrambe le viste; nessuna perdita di righe | US3 |

## Test automatici

```powershell
dotnet test --filter "FullyQualifiedName~DatePathTests|FullyQualifiedName~SessionTreeTests|FullyQualifiedName~SessionStoreTests"
```

## Cleanup

Ripristina `sessions.json` dal backup, `Remove-Item -Recurse C:\temp\rlv`.
