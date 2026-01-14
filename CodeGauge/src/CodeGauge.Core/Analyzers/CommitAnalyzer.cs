using CodeGauge.Core.Interfaces;
using CodeGauge.Core.Models;
using LibGit2Sharp;

namespace CodeGauge.Core.Analyzers;

public class CommitAnalyzer : IAnalyzer
{
    public string Name => "commit_hygiene";

    public Task<(int score, IEnumerable<FeedbackItem> feedback)> AnalyzeAsync(RepoContext context)
    {
        var feedback = new List<FeedbackItem>();
        int max = Weights.CommitHygiene;

        if (context.GitRepo == null)
        {
            feedback.Add(new FeedbackItem
            {
                Metric = Name,
                Score = 0,
                Issue = "Git repository not found.",
                Recommendation = "Initialize Git and commit regularly with descriptive messages."
            });
            return Task.FromResult(((int)(max * 0.1), feedback.AsEnumerable()));
        }

        var repo = context.GitRepo;
        // Count commits last 12 weeks
        int weeks = 12;
        DateTime since = DateTime.UtcNow.AddDays(-7 * weeks);
        var commits = repo.Commits.Where(c => c.Committer.When.UtcDateTime >= since).ToList();
        double perWeek = commits.Count / (double)weeks;

        // Score based on frequency and message quality
        int score = (int)(max * Math.Min(1.0, perWeek / 5.0)); // 5+ commits/week ~= full points

        int hugeCommits = 0;
        int poorMessages = 0;
        foreach (var c in commits)
        {
            var msg = c.MessageShort ?? string.Empty;
            if (string.IsNullOrWhiteSpace(msg) || msg.Length < 8)
                poorMessages++;

            // Heuristic: large diffs using Patch
            try
            {
                var parent = c.Parents.FirstOrDefault();
                if (parent != null)
                {
                    var patch = repo.Diff.Compare<Patch>(parent.Tree, c.Tree);
                    int lines = patch.LinesAdded + patch.LinesDeleted;
                    if (lines > 500) hugeCommits++;
                }
            }
            catch { }
        }

        if (poorMessages > 0)
        {
            feedback.Add(new FeedbackItem
            {
                Metric = Name,
                Score = score,
                Issue = $"{poorMessages} commit(s) with weak messages.",
                Recommendation = "Use descriptive, imperative commit messages (e.g., 'Add tests for parser')."
            });
            score = Math.Max(0, score - Math.Min(5, poorMessages));
        }

        if (hugeCommits > 0)
        {
            feedback.Add(new FeedbackItem
            {
                Metric = Name,
                Score = score,
                Issue = $"{hugeCommits} huge commit(s) (>500 lines) detected.",
                Recommendation = "Split large changes into smaller, reviewable commits."
            });
            score = Math.Max(0, score - Math.Min(5, hugeCommits));
        }

        return Task.FromResult((score, feedback.AsEnumerable()));
    }
}
