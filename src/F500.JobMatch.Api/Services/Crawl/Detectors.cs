using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;

namespace F500.JobMatch.Api.Services.Crawl;

public enum AtsType
{
    Unknown,
    Workday,
    Greenhouse,
    Lever,
    SmartRecruiters,
    SuccessFactors,
    Taleo,
    Icims
}

public class Detectors
{
    private static readonly Regex WorkdayRegex = new("workday", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex GreenhouseRegex = new("greenhouse", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex LeverRegex = new("lever.co", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SmartRecruitersRegex = new("smartrecruiters", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SuccessFactorsRegex = new("successfactors", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TaleoRegex = new("taleo", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex IcimsRegex = new("icims", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<AtsType> DetectAsync(HttpClient client, string url, CancellationToken cancellationToken)
    {
        try
        {
            var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return AtsType.Unknown;
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            return DetectFromHtml(html);
        }
        catch
        {
            return AtsType.Unknown;
        }
    }

    public AtsType DetectFromHtml(string html)
    {
        var parser = new HtmlParser();
        try
        {
            var document = parser.ParseDocument(html);
            var text = document.DocumentElement?.OuterHtml ?? html;
            if (WorkdayRegex.IsMatch(text)) return AtsType.Workday;
            if (GreenhouseRegex.IsMatch(text)) return AtsType.Greenhouse;
            if (LeverRegex.IsMatch(text)) return AtsType.Lever;
            if (SmartRecruitersRegex.IsMatch(text)) return AtsType.SmartRecruiters;
            if (SuccessFactorsRegex.IsMatch(text)) return AtsType.SuccessFactors;
            if (TaleoRegex.IsMatch(text)) return AtsType.Taleo;
            if (IcimsRegex.IsMatch(text)) return AtsType.Icims;
        }
        catch
        {
            if (WorkdayRegex.IsMatch(html)) return AtsType.Workday;
            if (GreenhouseRegex.IsMatch(html)) return AtsType.Greenhouse;
            if (LeverRegex.IsMatch(html)) return AtsType.Lever;
            if (SmartRecruitersRegex.IsMatch(html)) return AtsType.SmartRecruiters;
            if (SuccessFactorsRegex.IsMatch(html)) return AtsType.SuccessFactors;
            if (TaleoRegex.IsMatch(html)) return AtsType.Taleo;
            if (IcimsRegex.IsMatch(html)) return AtsType.Icims;
        }

        return AtsType.Unknown;
    }
}
