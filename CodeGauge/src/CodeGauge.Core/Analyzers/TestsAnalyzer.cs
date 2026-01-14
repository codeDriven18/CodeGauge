using System.Diagnostics;
using CodeGauge.Core.Interfaces;
using CodeGauge.Core.Models;

namespace CodeGauge.Core.Analyzers;

public class TestsAnalyzer : IAnalyzer
{
    public string Name => "tests";

    public async Task<(int score, IEnumerable<FeedbackItem> feedback)> AnalyzeAsync(RepoContext context)
    {
        var feedback = new List<FeedbackItem>();
        int max = Weights.TestsCoverage;

        var testProjects = context.CsprojFiles.Where(p =>
            Path.GetFileName(p).Contains("Test", StringComparison.OrdinalIgnoreCase) ||
            File.ReadAllText(p).Contains("Microsoft.NET.Test.Sdk", StringComparison.OrdinalIgnoreCase)
        ).ToList();

        if (!testProjects.Any())
        {
            feedback.Add(new FeedbackItem
            {
                Metric = Name,
                Score = 0,
                Issue = "No test projects detected.",
                Recommendation = "Add unit tests using xUnit/NUnit and aim for 70%+ coverage."
            });
            return (score: (int)(max * 0.1), feedback);
        }

        // Try to run tests with coverage (with timeout to prevent hanging)
        double coverage = 0;
        bool ran = false;
        try
        {
            var sln = context.SolutionPath;
            if (sln != null && context.Verbose)
            {
                // Use XPlat Code Coverage collector with strict timeout
                var psi = new ProcessStartInfo("dotnet", $"test \"{sln}\" --collect:\"XPlat Code Coverage\" --nologo --no-restore")
                {
                    WorkingDirectory = Path.GetDirectoryName(sln)!,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };
                using var proc = Process.Start(psi)!;
                var outputTask = proc.StandardOutput.ReadToEndAsync();
                var errorTask = proc.StandardError.ReadToEndAsync();
                
                // Wait max 30 seconds
                if (proc.WaitForExit(30000))
                {
                    string output = await outputTask;
                    string error = await errorTask;
                    ran = proc.ExitCode == 0;

                    // Attempt to parse overall coverage from output (heuristic)
                    // Look for summary lines like: Total Lines: ... Covered: ... (XX.XX%)
                    var percentIdx = output.LastIndexOf('%');
                    if (percentIdx > 0)
                    {
                        // Scan backwards for space then parse number
                        int start = Math.Max(0, percentIdx - 6);
                        var snippet = output.Substring(start, Math.Min(6, percentIdx - start)).Trim();
                        if (double.TryParse(snippet, out var perc))
                        {
                            coverage = Math.Max(0, Math.Min(100, perc));
                        }
                    }
                }
                else
                {
                    // Timeout - kill process
                    try { proc.Kill(); } catch { }
                    ran = false;
                }
            }
        }
        catch
        {
            ran = false;
        }

        int score;
        if (!ran)
        {
            feedback.Add(new FeedbackItem
            {
                Metric = Name,
                Score = 0,
                Issue = "Tests detected but failed to run or coverage not found.",
                Recommendation = "Ensure `dotnet test` passes locally. Add coverage via `--collect: XPlat Code Coverage` or Coverlet."
            });
            score = (int)(max * 0.4);
        }
        else
        {
            // Map coverage to score (70% => 70% of max, cap at 100%)
            double factor = Math.Min(1.0, coverage / 100.0);
            score = (int)(max * factor);
            if (coverage < 70)
            {
                feedback.Add(new FeedbackItem
                {
                    Metric = Name,
                    Score = score,
                    Issue = $"Coverage below target: {coverage:F1}%.",
                    Recommendation = "Increase test coverage; prioritize critical modules and error paths."
                });
            }
        }

        // Detect skipped/broken tests heuristically by searching attributes
        int skipped = 0;
        foreach (var cs in context.CSharpFiles.Where(f => f.Contains("Test", StringComparison.OrdinalIgnoreCase)))
        {
            var text = File.ReadAllText(cs);
            skipped += RegexCount(text, @"[Ff]act\(\s*Skip");
            skipped += RegexCount(text, @"[Tt]est\(\s*Ignore");
        }
        if (skipped > 0)
        {
            feedback.Add(new FeedbackItem
            {
                Metric = Name,
                Score = Math.Max(0, score - Math.Min(5, skipped)),
                Issue = $"{skipped} skipped tests found.",
                Recommendation = "Unskip tests or track technical debt with issues."
            });
            score = Math.Max(0, score - Math.Min(5, skipped));
        }

        return (score, feedback);
    }

    private static int RegexCount(string text, string pattern)
    {
        try
        {
            return System.Text.RegularExpressions.Regex.Matches(text, pattern).Count;
        }
        catch { return 0; }
    }
}
