# F500.JobMatch

F500.JobMatch ingests a single resume, politely crawls Fortune 500 career sites for product-manager-family roles, normalizes and deduplicates postings, and ranks the top 10 matches with concise explanations. The solution provides a REST API, Razor Pages UI, and CLI for batch workflows.

## Prerequisites

- [.NET SDK 8.0](https://dotnet.microsoft.com/download) (the repository pins 8.0.401 via `global.json` to avoid inadvertently using newer preview SDKs that break WPF restores)
- SQLite (bundled with .NET via Microsoft.Data.Sqlite)

If the SDK is missing in your environment (for example, the online execution sandbox used for these exercises), follow the
[local development guide](docs/development.md) to install it with the official bootstrap script or by using the .NET SDK Docker
image before running the commands below.

## Getting Started

1. **Restore tools & build**
   ```bash
   dotnet restore
   ```

2. **Apply migrations** (creates `jobmatch.db` in the API project directory)
   ```bash
   dotnet ef database update --project src/F500.JobMatch.Api
   ```

3. **Run the API + UI**
   ```bash
   dotnet run --project src/F500.JobMatch.Api
   ```
   Browse to `https://localhost:5001` (or the configured URL) to upload a resume, run a crawl, and view matches.

### Configuration

`src/F500.JobMatch.Api/appsettings.json` contains crawler and scoring configuration:

```json
"Crawl": {
  "IgnoreRobots": false,
  "UserAgent": "f500-jobmatch-bot/1.0 (contact: you@example.com)",
  "MaxRpsPerHost": 0.5,
  "TimeoutSeconds": 30,
  "MaxPagesPerSite": 5,
  "TitleIncludeRegex": "(?i)(product\\s*manager|senior\\s*product...)"
},
"Matching": {
  "PreferredLocations": [ "Remote", "New York", "San Francisco", "Seattle", "Austin" ]
}
```

Update the Fortune 500 CSV path, crawler limits, or preferred locations as needed. `data/fortune500.sample.csv` ships with ~20 example companies; replace with a full list for production usage.

### CLI Usage

```bash
# ingest resume
dotnet run --project src/F500.JobMatch.Cli -- ingest-resume ./data/fixtures/resume_sample.txt

# crawl careers (limit 50 companies, skip already stored postings)
dotnet run --project src/F500.JobMatch.Cli -- crawl --csv data/fortune500.sample.csv --limit 50

# score matches
dotnet run --project src/F500.JobMatch.Cli -- match <resumeId> --top 10
```

### Desktop App

```bash
dotnet run --project src/F500.JobMatch.Desktop
```

Use the desktop interface to choose a resume file, ingest it into the database, and review the ranked matches and explanations wi
thin the same window.

### Respecting Terms of Service

The crawler honors `robots.txt` by default, uses a configurable user agent, and includes conservative rate limits and page depth. If a site blocks bot access or requires authentication/CAPTCHA, the crawl is skipped and reported.

### Improving Relevance

Potential enhancements include:

- Swap TF-IDF with BM25 or hybrid TF-IDF/BM25.
- Add ONNX-backed semantic embeddings or re-rankers.
- Enhance ATS-specific adapters with deeper pagination and API integrations.
- Persist crawl telemetry and resume/job attachments in blob storage.

## Testing

Run the automated tests with:

```bash
dotnet test
```

Unit tests cover TF-IDF scoring, ATS detection heuristics, and normalization.
