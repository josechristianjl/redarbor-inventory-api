using InventoryManagement.Api.Contracts.Products;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagement.IntegrationTests.ErrorHandling;

[ApiController]
[Route("test/errors")]
public sealed class HttpErrorProbeController : ControllerBase
{
    [HttpPost("write")]
    public IActionResult Write([FromBody] ProductWriteRequest request)
    {
        return Ok(request);
    }

    [HttpGet("query")]
    public IActionResult Query([FromQuery] int page)
    {
        return Ok(page);
    }
}
