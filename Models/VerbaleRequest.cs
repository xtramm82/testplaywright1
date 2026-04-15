using System.ComponentModel.DataAnnotations;

namespace testplaywright1.Models;

public sealed record VerbaleRequest(
    [property: Required, MinLength(1)] string Targa,
    [property: Required, MinLength(1)] string NumeroVerbale);
