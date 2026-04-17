using System.ComponentModel.DataAnnotations;

namespace testplaywright1.Models;

public class VerbaleRequest()
{
    public string Targa { get; set; }
    public string NumeroVerbale { get; set; }
    public decimal Importo { get; set; }
    public bool ForzaPagamento { get; set; }
    public string EmailNotifica{ get; set; }
}