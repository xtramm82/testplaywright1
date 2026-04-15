using testplaywright1.Models;

namespace testplaywright1.Services;

public sealed class VerbaleStore
{
    private readonly object _sync = new();

    public string? LastTarga { get; private set; }
    public string? LastNumeroVerbale { get; private set; }
    public DateTimeOffset? LastDataRicevimento { get; private set; }

    public VerbaleResponse Save(string targa, string numeroVerbale, DateTimeOffset dataRicevimento)
    {
        lock (_sync)
        {
            LastTarga = targa;
            LastNumeroVerbale = numeroVerbale;
            LastDataRicevimento = dataRicevimento;
        }

        return new VerbaleResponse(
            targa,
            numeroVerbale,
            dataRicevimento,
            $"Ricevuti targa '{targa}' e numero verbale '{numeroVerbale}'.");
    }
}
