using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using F500.JobMatch.Api.Services;
using F500.JobMatch.Api.Services.Match;
using Microsoft.AspNetCore.Http;
using Microsoft.Win32;

namespace F500.JobMatch.Desktop;

public partial class MainWindow : Window
{
    private readonly ResumeService _resumeService;
    private readonly MatchScoring _matchScoring;
    private readonly ExplainService _explainService;
    private bool _isBusy;

    public ObservableCollection<MatchViewModel> Results { get; } = new();

    public MainWindow(ResumeService resumeService, MatchScoring matchScoring, ExplainService explainService)
    {
        _resumeService = resumeService;
        _matchScoring = matchScoring;
        _explainService = explainService;

        InitializeComponent();
        DataContext = this;
    }

    private void BrowseButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Filter = "Resumes (*.pdf;*.doc;*.docx;*.txt)|*.pdf;*.doc;*.docx;*.txt|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            FilePathTextBox.Text = dialog.FileName;
            StatusTextBlock.Text = "Resume selected. Ready to ingest.";
        }
    }

    private async void IngestButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        var filePath = FilePathTextBox.Text;
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            StatusTextBlock.Text = "Please select a resume file before ingesting.";
            return;
        }

        if (!int.TryParse(TopResultsTextBox.Text, out var top) || top <= 0)
        {
            top = 10;
            TopResultsTextBox.Text = top.ToString();
        }

        SetBusy(true);

        try
        {
            StatusTextBlock.Text = "Saving resume…";
            await using var stream = File.OpenRead(filePath);
            var formFile = new FormFile(stream, 0, stream.Length, "file", Path.GetFileName(filePath));
            var resumeId = await _resumeService.SaveResumeAsync(formFile);

            StatusTextBlock.Text = $"Resume stored. Calculating top {top} matches…";
            var resume = await _resumeService.GetResumeAsync(resumeId);
            if (resume is null)
            {
                StatusTextBlock.Text = "Unable to load resume after ingest.";
                return;
            }

            var matches = await _matchScoring.ScoreTopAsync(resumeId, top, CancellationToken.None);
            Results.Clear();
            foreach (var match in matches)
            {
                var bullets = _explainService.BuildExplanation(match, resume).ToArray();
                Results.Add(new MatchViewModel(match.Posting.Title, match.Posting.Company, match.Score, match.Posting.Url ?? string.Empty, bullets));
            }

            StatusTextBlock.Text = Results.Count == 0
                ? "No matches available for this resume yet."
                : $"Displaying {Results.Count} matches for resume {resumeId}.";
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"Error: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        BrowseButton.IsEnabled = !busy;
        IngestButton.IsEnabled = !busy;
    }
}

public class MatchViewModel
{
    public MatchViewModel(string title, string company, double score, string url, string[] explanation)
    {
        Title = title;
        Company = company;
        Score = score;
        Url = url;
        ExplanationLines = explanation;
    }

    public string Title { get; }
    public string Company { get; }
    public double Score { get; }
    public string Url { get; }
    public string[] ExplanationLines { get; }

    public string TitleLine => $"{Title} @ {Company}";
    public string ScoreLine => $"Score: {Score:F1}";
    public string Explanation => ExplanationLines.Length == 0 ? "" : string.Join(Environment.NewLine, ExplanationLines);
}
