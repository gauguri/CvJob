using System.Linq;
using F500.JobMatch.Api.Models;
using F500.JobMatch.Api.Services;
using F500.JobMatch.Api.Services.Match;
using Microsoft.AspNetCore.Mvc;

namespace F500.JobMatch.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MatchController : ControllerBase
{
    private readonly MatchScoring _matchScoring;
    private readonly ExplainService _explainService;
    private readonly ResumeService _resumeService;

    public MatchController(MatchScoring matchScoring, ExplainService explainService, ResumeService resumeService)
    {
        _matchScoring = matchScoring;
        _explainService = explainService;
        _resumeService = resumeService;
    }

    [HttpGet("top10")]
    [ProducesResponseType(typeof(IEnumerable<MatchResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTopMatches([FromQuery] Guid resumeId, CancellationToken cancellationToken)
    {
        var resume = await _resumeService.GetResumeAsync(resumeId, cancellationToken);
        if (resume == null)
        {
            return NotFound();
        }

        var scores = await _matchScoring.ScoreTopAsync(resumeId, 10, cancellationToken);
        var results = scores.Select(score => new MatchResultDto
        {
            Title = score.Posting.Title,
            Company = score.Posting.Company,
            Location = score.Posting.Location,
            Url = score.Posting.Url,
            Source = score.Posting.Source,
            MatchScore = Math.Round(score.Score, 1),
            Explanation = _explainService.BuildExplanation(score, resume)
        }).ToList();

        return Ok(results);
    }
}
