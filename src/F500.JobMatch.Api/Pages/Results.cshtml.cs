using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace F500.JobMatch.Api.Pages;

public class ResultsModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid ResumeId { get; set; }

    public void OnGet()
    {
    }
}
