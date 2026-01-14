namespace CodeGauge.Core.Interfaces;

using CodeGauge.Core.Models;

public interface IAnalyzer
{
    string Name { get; }
    Task<(int score, IEnumerable<FeedbackItem> feedback)> AnalyzeAsync(CodeGauge.Core.RepoContext context);
}
