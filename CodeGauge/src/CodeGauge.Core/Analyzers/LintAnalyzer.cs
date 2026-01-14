using System.Diagnostics;
using CodeGauge.Core.Interfaces;
using CodeGauge.Core.Models;

namespace CodeGauge.Core.Analyzers;

public class LintAnalyzer : IAnalyzer
{
    public string Name => "lint_formatting";

    public async Task<(int score, IEnumerable<FeedbackItem> feedback)> AnalyzeAsync(RepoContext context)
    {
        var feedback = new List<FeedbackItem>();
        int max = Weights.LintFormatting;

        // Try dotnet format verify if solution exists
        if (context.SolutionPath is string sln)
        {
            try
            {
                var psi = new ProcessStartInfo("dotnet", $"format --verify-no-changes \"{sln}\" --severity info --no-restore")
                {
                    WorkingDirectory = Path.GetDirectoryName(sln)!,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };
                using var proc = Process.Start(psi)!;
                var outputTask = proc.StandardOutput.ReadToEndAsync();
                var errorTask = proc.StandardError.ReadToEndAsync();
                
                // Wait max 20 seconds
                if (proc.WaitForExit(20000))
                {
                    string output = await outputTask;
                    string error = await errorTask;
                    if (proc.ExitCode == 0)
                    {
                        return (max, feedback);
                    }
                    else
                    {
                        feedback.Add(new FeedbackItem
                        {
                            Metric = Name,
                            Score = 0,
                            Issue = "Formatting or style issues detected by dotnet-format.",
                            Recommendation = "Run `dotnet format` and add `.editorconfig` with consistent style rules."
                        });
                        return ((int)(max * 0.4), feedback);
                    }
                }
                else
                {
                    // Timeout - kill and fallback
                    try { proc.Kill(); } catch { }
                }
            }
            catch
            {
                // fallback heuristic
            }
        }

        // Heuristic: basic indentation/naming checks
        int violations = 0;
        foreach (var file in context.CSharpFiles)
        {
            foreach (var line in File.ReadLines(file))
            {
                if (line.Contains("\t")) violations++; // prefer spaces
                if (line.Length > 160) violations++;
            }
        }
        int score = Math.Max(0, max - Math.Min(max, violations / 200));
        if (violations > 0)
        {
            feedback.Add(new FeedbackItem
            {
                Metric = Name,
                Score = score,
                Issue = $"Formatting/naming heuristic flagged {violations} issue(s).",
                Recommendation = "Adopt consistent style via .editorconfig and enforce CI lint checks."
            });
        }

        return (score, feedback);
    }
}
