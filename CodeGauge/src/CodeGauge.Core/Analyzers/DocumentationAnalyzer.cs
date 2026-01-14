using CodeGauge.Core.Interfaces;
using CodeGauge.Core.Models;

namespace CodeGauge.Core.Analyzers;

public class DocumentationAnalyzer : IAnalyzer
{
    public string Name => "documentation";

    public Task<(int score, IEnumerable<FeedbackItem> feedback)> AnalyzeAsync(RepoContext context)
    {
        var feedback = new List<FeedbackItem>();
        int max = Weights.Documentation;

        var readme = Directory.GetFiles(context.RootPath, "README*", SearchOption.TopDirectoryOnly).FirstOrDefault();
        int score = 0;
        if (readme == null)
        {
            feedback.Add(new FeedbackItem
            {
                Metric = Name,
                Score = 0,
                Issue = "README not found.",
                Recommendation = "Add README with overview, setup, usage, and contribution guide."
            });
            score = (int)(max * 0.2);
        }
        else
        {
            var text = File.ReadAllText(readme);
            int sections = 0;
            if (text.Contains("Install", StringComparison.OrdinalIgnoreCase)) sections++;
            if (text.Contains("Usage", StringComparison.OrdinalIgnoreCase)) sections++;
            if (text.Contains("Contribut", StringComparison.OrdinalIgnoreCase)) sections++;
            if (text.Contains("License", StringComparison.OrdinalIgnoreCase)) sections++;
            score = (int)(max * Math.Min(1.0, 0.25 * sections + 0.25));
            if (sections < 3)
            {
                feedback.Add(new FeedbackItem
                {
                    Metric = Name,
                    Score = score,
                    Issue = "README missing key sections.",
                    Recommendation = "Include installation, usage examples, contribution guide, and license."
                });
            }
        }

        // Inline comments proportion
        int totalLines = 0;
        int commentLines = 0;
        foreach (var file in context.CSharpFiles)
        {
            var lines = File.ReadAllLines(file);
            totalLines += lines.Length;
            commentLines += lines.Count(l => l.TrimStart().StartsWith("//") || l.Contains("/*"));
        }
        if (totalLines > 0)
        {
            double ratio = commentLines / (double)totalLines;
            if (ratio < 0.03)
            {
                feedback.Add(new FeedbackItem
                {
                    Metric = Name,
                    Score = score,
                    Issue = "Very low inline comments (<3%).",
                    Recommendation = "Add brief comments for complex logic and public APIs."
                });
                score = Math.Max(0, score - 1);
            }
        }

        return Task.FromResult((score, feedback.AsEnumerable()));
    }
}
