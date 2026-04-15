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

    [HttpPost]
    public ActionResult<VerbaleResponse> Post([FromBody] VerbaleRequest request)
    {
        var ricevutoIl = DateTimeOffset.Now;
        var response = _store.Save(request.Targa, request.NumeroVerbale, ricevutoIl);
        return Ok(response);
    }
}
