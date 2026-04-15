# testplaywright1

Web API .NET 8 che riceve `targa` e `numeroVerbale`, li salva in memoria e risponde con un JSON di riepilogo e data di ricevimento.

## Endpoint

`GET /api/verbali/VerificaImporto?targa=AB123CD&numeroVerbale=12345`

### Risposta

```json
{
  "targa": "AB123CD",
  "numeroVerbale": "12345",
  "dataRicevimento": "2026-04-15T22:56:00+02:00",
  "riassunto": "Ricevuti targa 'AB123CD' e numero verbale '12345'."
}
```

## Note

- I dati vengono tenuti solo in memoria tramite `VerbaleStore`.
- Il progetto include `Microsoft.Playwright` per le integrazioni future di scraping.
- Per eseguire davvero il progetto serve installare .NET 8 nel sistema.
