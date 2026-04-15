using Microsoft.AspNetCore.Mvc;
using testplaywright1.Models;
using testplaywright1.Services;

namespace testplaywright1.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class VerbaliController : ControllerBase
{
    private readonly VerbaleStore _store;

    public VerbaliController(VerbaleStore store)
    {
        _store = store;
    }

    [HttpGet("VerificaImporto")]
    public ActionResult<VerbaleResponse> VerificaImporto([FromQuery] string targa, [FromQuery] string numeroVerbale)
    {
        var ricevutoIl = DateTimeOffset.Now;
        var response = _store.Save(targa, numeroVerbale, ricevutoIl);
        return Ok(response);
    }
}
