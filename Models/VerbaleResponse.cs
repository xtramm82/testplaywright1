namespace testplaywright1.Models;

public sealed record VerbaleResponse(
    string Targa,
    string NumeroVerbale,
    DateTimeOffset DataRicevimento,
    string Riassunto);
