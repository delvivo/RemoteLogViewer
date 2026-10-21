# Quickstart: validazione Analisi IA

## Prerequisiti

- `dotnet build` ok; almeno una CLI installata e autenticata (es. `claude`) per i passi 3–6; il passo 2 non richiede CLI IA.
- File di prova: un `.log` locale con alcune righe `ERROR` ripetute e righe con accenti (`è`, `à`).

## 1. Test automatici

```powershell
dotnet test --filter "FullyQualifiedName~Ai|FullyQualifiedName~MarkdownHtml"
```

Atteso: verdi `AiInputTests` (limite, water-filling, righe recenti, intestazioni), `AiSettingsTests` (default, round-trip, file corrotto, ripristina), `MarkdownHtmlTests` (titoli, elenchi, tabelle, code block, escaping), `AiRunnerTests` (eco via `findstr "^"`, codice 9009, annullamento).

## 2. Flusso senza IA (comando personalizzato)

1. `dotnet run --project src/RemoteLogViewer`, aprire il file di prova (Ctrl+O).
2. Ctrl+Maiusc+I → finestra "Analisi IA"; la scheda è spuntata.
3. Strumento **Personalizzato**, comando `findstr /n "^"`; Avvia → conferma consenso (Sì).
4. Atteso: la risposta mostra prompt e righe numerate, accenti corretti; stato "Completato".
5. Comando `nonesiste` → stato "Errore", messaggio "Comando non trovato: nonesiste".
6. Comando `ping -n 30 127.0.0.1` → Annulla entro 2 s → stato "Annullato", output parziale visibile, nessun `ping.exe` residuo in Gestione attività.

## 3. Report errori con Claude Code

1. Strumento **Claude Code**, prompt **Report errori**, Avvia → consenso (prima volta).
2. Atteso: report Markdown con errori raggruppati, occorrenze, prima/ultima occorrenza, cause e azioni.
3. Riavviare l'analisi: nessuna nuova richiesta di consenso.

## 4. Export e formattato

1. **Apri formattato** → browser con titoli/tabelle resi, offline.
2. **Esporta…** → `.md` e poi `.html`: intestazione (data, strumento, schede, prompt) + risposta integrale.

## 5. Più schede e limite

1. Aprire due schede (una con filtro livello ERROR attivo), spuntarle entrambe, comando personalizzato `findstr /n "^"`.
2. Atteso: due sezioni `=== nome (percorso) ===`; la scheda filtrata contiene solo le righe visibili.
3. Con un file > 1 MB: stato indica "N righe escluse"; nel testo inviato restano le righe più recenti.

## 6. Persistenza e lingua

1. Modificare il comando di Codex, scrivere un prompt libero, chiudere l'app, riaprire: strumento, comando e prompt ritrovati. **Ripristina** riporta il predefinito.
2. Cambiare lingua (it ↔ en) con la finestra aperta: etichette e stato aggiornati.
3. Pulizia: cancellare `%APPDATA%\RemoteLogViewer\ai.json` se creato durante i test manuali.
