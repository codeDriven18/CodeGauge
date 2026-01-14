using CodeGauge.Core.Interfaces;
using CodeGauge.Core.Models;

namespace CodeGauge.Core.Engine;

public class AnalysisEngine
{
    private readonly List<IAnalyzer> _analyzers = new();

    public AnalysisEngine(IEnumerable<IAnalyzer> analyzers)
    {
        _analyzers.AddRange(analyzers);
    }

    public static AnalysisEngine CreateDefault()
    {
        // Register core analyzers only; language-specific analyzers added by CLI.
        var list = new List<IAnalyzer>
        {
            new Analyzers.TestsAnalyzer(),
            new Analyzers.CommitAnalyzer(),
            new Analyzers.DocumentationAnalyzer(),
            new Analyzers.DependencyAnalyzer(),
            new Analyzers.LintAnalyzer(),
        };
        return new AnalysisEngine(list);
    }

    public async Task<ScoreResult> RunAsync(RepoContext context)
    {
        var result = new ScoreResult
        {
            RepoName = new DirectoryInfo(context.RootPath).Name,
            PrimaryLanguage = "C#",
        };

        var categoryScores = new CategoryScores();
        var feedback = new List<FeedbackItem>();

        foreach (var analyzer in _analyzers)
        {
            var (score, analyzerFeedback) = await analyzer.AnalyzeAsync(context);
            switch (analyzer.Name)
            {
                case "tests":
                    categoryScores.Tests = score; break;
                case "code_quality":
                    categoryScores.CodeQuality = score; break;
                case "commit_hygiene":
                    categoryScores.CommitHygiene = score; break;
                case "documentation":
                    categoryScores.Documentation = score; break;
                case "dependencies":
                    categoryScores.Dependencies = score; break;
                case "error_handling":
                    categoryScores.ErrorHandling = score; break;
                case "lint_formatting":
                    categoryScores.LintFormatting = score; break;
            }
            feedback.AddRange(analyzerFeedback);
        }

        // Total score is sum of weighted category scores already normalized to weights
        result.CategoryScores = categoryScores;
        result.TotalScore = categoryScores.Tests
                          + categoryScores.CodeQuality
                          + categoryScores.CommitHygiene
                          + categoryScores.Documentation
                          + categoryScores.Dependencies
                          + categoryScores.ErrorHandling
                          + categoryScores.LintFormatting;

        result.Feedback = feedback;
        result.SummaryBadge = new SummaryBadge
        {
            Score = result.TotalScore,
            Color = result.TotalScore >= 76 ? "green" : result.TotalScore >= 51 ? "yellow" : "red"
        };

        return result;
    }
}
