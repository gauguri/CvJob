namespace F500.JobMatch.Api.Services.Crawl;

/// <summary>
///     Minimal robots.txt parser capable of evaluating Allow/Disallow rules for a user agent.
/// </summary>
internal sealed class RobotsRules
{
    private readonly Dictionary<string, List<Rule>> _rules;

    private RobotsRules(Dictionary<string, List<Rule>> rules)
    {
        _rules = rules;
    }

    public static RobotsRules Parse(string content)
    {
        var rules = new Dictionary<string, List<Rule>>(StringComparer.OrdinalIgnoreCase);
        var currentAgents = new List<string>();
        var lastDirectiveWasUserAgent = false;

        foreach (var rawLine in content.Split(['\n']))
        {
            var line = StripComments(rawLine).Trim();
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            var separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var directive = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();

            switch (directive.ToLowerInvariant())
            {
                case "user-agent":
                    if (!lastDirectiveWasUserAgent)
                    {
                        currentAgents.Clear();
                    }

                    lastDirectiveWasUserAgent = true;

                    if (string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }

                    var agent = value.ToLowerInvariant();
                    currentAgents.Add(agent);
                    EnsureRuleList(rules, agent);
                    break;

                case "allow":
                case "disallow":
                    lastDirectiveWasUserAgent = false;
                    if (currentAgents.Count == 0)
                    {
                        currentAgents.Add("*");
                        EnsureRuleList(rules, "*");
                    }

                    var isAllow = directive.Equals("allow", StringComparison.OrdinalIgnoreCase);
                    if (!isAllow && string.IsNullOrEmpty(value))
                    {
                        // "Disallow:" without a path means everything is allowed.
                        continue;
                    }

                    var rule = new Rule(isAllow, value);
                    foreach (var currentAgent in currentAgents)
                    {
                        rules[currentAgent].Add(rule);
                    }

                    break;
            }
        }

        if (rules.Count == 0)
        {
            rules["*"] = new List<Rule>();
        }

        return new RobotsRules(rules);
    }

    public bool IsPathAllowed(string userAgent, string path)
    {
        if (path is null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        userAgent = string.IsNullOrWhiteSpace(userAgent) ? "*" : userAgent.ToLowerInvariant();

        var matchingRules = GetRulesForAgent(userAgent);
        var bestMatch = default(Rule?);
        var bestMatchLength = -1;

        foreach (var rule in matchingRules)
        {
            if (!rule.AppliesTo(path))
            {
                continue;
            }

            if (rule.Path.Length > bestMatchLength)
            {
                bestMatch = rule;
                bestMatchLength = rule.Path.Length;
            }
        }

        return bestMatch?.Allow ?? true;
    }

    private IEnumerable<Rule> GetRulesForAgent(string userAgent)
    {
        if (_rules.TryGetValue(userAgent, out var specificRules))
        {
            foreach (var rule in specificRules)
            {
                yield return rule;
            }
        }

        if (!userAgent.Equals("*", StringComparison.OrdinalIgnoreCase) && _rules.TryGetValue("*", out var wildcardRules))
        {
            foreach (var rule in wildcardRules)
            {
                yield return rule;
            }
        }
    }

    private static void EnsureRuleList(Dictionary<string, List<Rule>> rules, string agent)
    {
        if (!rules.ContainsKey(agent))
        {
            rules[agent] = new List<Rule>();
        }
    }

    private static string StripComments(string line)
    {
        var hashIndex = line.IndexOf('#');
        return hashIndex >= 0 ? line[..hashIndex] : line;
    }

    private readonly record struct Rule(bool Allow, string Path)
    {
        public bool AppliesTo(string candidatePath)
        {
            if (string.IsNullOrEmpty(Path))
            {
                return true;
            }

            if (!candidatePath.StartsWith('/'))
            {
                candidatePath = "/" + candidatePath.TrimStart('/');
            }

            var rulePath = Path.StartsWith('/') ? Path : "/" + Path.TrimStart('/');

            return candidatePath.StartsWith(rulePath, StringComparison.Ordinal);
        }
    }
}
