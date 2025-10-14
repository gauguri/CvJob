using System.Linq;
namespace F500.JobMatch.Api.Services.Match;

public class TfIdfVectorizer
{
    private readonly TextClean _clean;

    public TfIdfVectorizer(TextClean clean)
    {
        _clean = clean;
    }

    public IReadOnlyList<string> Tokenize(string text) => _clean.Tokenize(text);

    public Dictionary<string, double> ComputeTf(IReadOnlyList<string> tokens)
    {
        var tf = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        if (tokens.Count == 0)
        {
            return tf;
        }
        var total = tokens.Count;
        foreach (var token in tokens)
        {
            tf[token] = tf.TryGetValue(token, out var value) ? value + 1 : 1;
        }
        foreach (var key in tf.Keys.ToList())
        {
            tf[key] /= total;
        }
        return tf;
    }

    public Dictionary<string, double> ComputeIdf(IEnumerable<IReadOnlyList<string>> documents)
    {
        var docCount = 0;
        var df = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var doc in documents)
        {
            docCount++;
            foreach (var token in doc.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                df[token] = df.TryGetValue(token, out var value) ? value + 1 : 1;
            }
        }

        var idf = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in df)
        {
            idf[kvp.Key] = Math.Log((double)(docCount + 1) / (kvp.Value + 1)) + 1;
        }
        return idf;
    }

    public Dictionary<string, double> ComputeTfIdf(IReadOnlyList<string> tokens, IReadOnlyDictionary<string, double> idf)
    {
        var tf = ComputeTf(tokens);
        var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in tf)
        {
            var weight = idf.TryGetValue(kvp.Key, out var idfWeight) ? idfWeight : 0;
            result[kvp.Key] = kvp.Value * idfWeight;
        }
        return result;
    }

    public double CosineSimilarity(IReadOnlyDictionary<string, double> vectorA, IReadOnlyDictionary<string, double> vectorB)
    {
        double dot = 0;
        double magnitudeA = 0;
        double magnitudeB = 0;

        foreach (var value in vectorA.Values)
        {
            magnitudeA += value * value;
        }

        foreach (var value in vectorB.Values)
        {
            magnitudeB += value * value;
        }

        foreach (var kvp in vectorA)
        {
            if (vectorB.TryGetValue(kvp.Key, out var other))
            {
                dot += kvp.Value * other;
            }
        }

        if (magnitudeA == 0 || magnitudeB == 0)
        {
            return 0;
        }

        return dot / (Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB));
    }
}
