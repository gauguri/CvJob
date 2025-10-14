using Microsoft.AspNetCore.Mvc;
using F500.JobMatch.Api.Models;
using F500.JobMatch.Api.Services;

namespace F500.JobMatch.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ResumeController : ControllerBase
{
    private readonly ResumeService _resumeService;

    public ResumeController(ResumeService resumeService)
    {
        _resumeService = resumeService;
    }

    [HttpPost("upload")]
    [ProducesResponseType(typeof(ResumeUploadResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UploadResume([FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        var id = await _resumeService.SaveResumeAsync(file, cancellationToken);
        return Ok(new ResumeUploadResponse(id));
    }
}
