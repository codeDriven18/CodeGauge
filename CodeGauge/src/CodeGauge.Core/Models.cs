namespace CodeGauge.Core.Models;

public class CategoryScores
{
    public int Tests { get; set; }
    public int CodeQuality { get; set; }
    public int CommitHygiene { get; set; }
    public int Documentation { get; set; }
    public int Dependencies { get; set; }
    public int ErrorHandling { get; set; }
    public int LintFormatting { get; set; }
}

public class FeedbackItem
{
    public string Metric { get; set; } = string.Empty;
    public int Score { get; set; }
    public string Issue { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}

public class SummaryBadge
{
    public int Score { get; set; }
    public string Color { get; set; } = "red";
}

public class ScoreResult
{
    public string RepoName { get; set; } = string.Empty;
    public string PrimaryLanguage { get; set; } = "C#";
    public int TotalScore { get; set; }
    public CategoryScores CategoryScores { get; set; } = new();
    public List<FeedbackItem> Feedback { get; set; } = new();
    public SummaryBadge SummaryBadge { get; set; } = new();
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
}

public static class Weights
{
    public const int TestsCoverage = 25;
    public const int CodeQuality = 25;
    public const int CommitHygiene = 15;
    public const int Documentation = 10;
    public const int Dependencies = 10;
    public const int ErrorHandling = 10;
    public const int LintFormatting = 5;
}
