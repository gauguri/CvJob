using Microsoft.AspNetCore.Mvc.RazorPages;

namespace F500.JobMatch.Api.Pages;

public class IndexModel : PageModel
{
    public string DefaultCsv { get; private set; } = "data/fortune500.sample.csv";

    public void OnGet()
    {
    }
}
