using F500.JobMatch.Api.Models;
using F500.JobMatch.Api.Services.Crawl;
using Microsoft.AspNetCore.Mvc;

namespace F500.JobMatch.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CrawlController : ControllerBase
{
    private readonly CrawlDispatcher _dispatcher;

    public CrawlController(CrawlDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [HttpPost("run")]
    [ProducesResponseType(typeof(CrawlResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Run([FromBody] CrawlRequest request, CancellationToken cancellationToken)
    {
        var summaries = await _dispatcher.RunAsync(request, cancellationToken);
        return Ok(new CrawlResponse(summaries));
    }
}
