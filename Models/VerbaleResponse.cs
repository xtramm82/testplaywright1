namespace testplaywright1.Models;

public class VerbaleResponse()
{
    public string Targa { get; set; }
    public string NumeroVerbale { get; set; }
    public decimal Importo { get; set; }
    public decimal? ImportoDaPagare { get; set; }
    public DateTime DataRicevimento { get; set; }
    public string ContenutoPagina { get; set; }
    public string CurrentUrl { get; set; }
    public string Errore { get; set; }
}